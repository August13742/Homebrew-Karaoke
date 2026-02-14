using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PitchGame
{
    public partial class SongImporter : Node
    {
        [Signal] public delegate void ImportStartedEventHandler(string fileName);
        [Signal] public delegate void ImportProgressEventHandler(string stage, float progress);
        [Signal] public delegate void ImportLogEventHandler(string message);
        [Signal] public delegate void ImportCompletedEventHandler(string songName, bool success);

        private readonly string[] _supportedExtensions = { "mp3", "wav", "flac", "ogg", "m4a", "aac" };
        private bool _isImporting = false;
        private System.Diagnostics.Process _currentProcess;

        public void CancelImport()
        {
            if (_currentProcess != null && !_currentProcess.HasExited)
            {
                try {
                    _currentProcess.Kill(true); // Kill entire tree (uv + python)
                    GD.Print("[SongImporter] Import cancelled by user.");
                } catch { }
            }
        }

        public void ProcessFiles(string[] files)
        {
            if (_isImporting)
            {
                GD.Print("[SongImporter] Already importing, ignoring files.");
                return;
            }

            if (files.Length == 0) return;

            string filePath = files[0]; // Process first file for now
            string ext = filePath.GetExtension().ToLower();

            bool supported = false;
            foreach (var s in _supportedExtensions)
            {
                if (ext == s)
                {
                    supported = true;
                    break;
                }
            }

            if (!supported)
            {
                GD.PrintErr($"[SongImporter] Unsupported file format: {ext}");
                EmitSignal(SignalName.ImportLog, $"Error: Unsupported format .{ext}");
                return;
            }

            Task.Run(() => RunPipeline(filePath));
        }

        private async Task RunPipeline(string inputPath)
        {
            _isImporting = true;
            string fileName = Path.GetFileNameWithoutExtension(inputPath);
            string songName = fileName.Replace(" ", "_"); // Basic sanitization
            
            // Output directory inside the game's Music folder
            string musicPath = ProjectSettings.GlobalizePath("res://Music");
            string outputDir = Path.Combine(musicPath); // main.py karaoke creates a subfolder by default

            GD.Print($"[SongImporter] Importing {fileName}");
            GD.Print($"[SongImporter] Input: {inputPath}");
            GD.Print($"[SongImporter] Output directory: {outputDir}");
            
            CallDeferred(MethodName.EmitSignal, SignalName.ImportStarted, fileName);
            CallDeferred(MethodName.EmitSignal, SignalName.ImportLog, $"Starting import of {fileName}...");

            try
            {
                // Prepare arguments for 'uv run main.py karaoke'
                string pythonRoot = ProjectSettings.GlobalizePath("res://Audio-Processing-Utilities");
                string mainPy = Path.Combine(pythonRoot, "main.py");
                
                if (!File.Exists(inputPath))
                {
                    throw new FileNotFoundException($"Input file not found: {inputPath}");
                }
                
                if (!Directory.Exists(pythonRoot))
                {
                    throw new DirectoryNotFoundException($"Python utilities directory not found: {pythonRoot}");
                }

                string[] args = { 
                    "run", 
                    mainPy, 
                    "karaoke", 
                    inputPath, 
                    outputDir 
                };

                GD.Print($"[SongImporter] Python root: {pythonRoot}");
                GD.Print($"[SongImporter] Arguments: {string.Join(" ", args)}");
                
                await RunProcessWithOutput("uv", args, pythonRoot);
                
                CallDeferred(MethodName.EmitSignal, SignalName.ImportLog, $"[color=green]Import completed successfully![/color]");
                CallDeferred(MethodName.EmitSignal, SignalName.ImportCompleted, songName, true);
            }
            catch (Exception e)
            {
                GD.PrintErr($"[SongImporter] Error during import: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                CallDeferred(MethodName.EmitSignal, SignalName.ImportLog, $"[color=red]Error: {e.Message}[/color]");
                CallDeferred(MethodName.EmitSignal, SignalName.ImportCompleted, songName, false);
            }
            finally
            {
                _isImporting = false;
            }
        }

        private async Task RunProcessWithOutput(string command, string[] args, string workingDir)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = command,
                Arguments = string.Join(" ", QuoteArgs(args)),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDir
            };

            try
            {
                _currentProcess = new System.Diagnostics.Process { StartInfo = startInfo };
                
                // Log the command being executed
                GD.Print($"[SongImporter] Executing: {command} {string.Join(" ", QuoteArgs(args))}");
                GD.Print($"[SongImporter] Working directory: {workingDir}");
                
                if (!_currentProcess.Start())
                {
                    throw new Exception("Failed to start process");
                }

                var errors = new List<string>();
                var errorsCopy = errors; // Capture for closure

                // Handle stderr for progress and logs
                var errorTask = Task.Run(async () =>
                {
                    try
                    {
                        string line;
                        while ((line = await _currentProcess.StandardError.ReadLineAsync()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;

                            errorsCopy.Add(line);

                            try
                            {
                                // Try to parse as JSON progress
                                if (line.StartsWith("{"))
                                {
                                    var doc = JsonDocument.Parse(line);
                                    if (doc.RootElement.TryGetProperty("type", out var type) && type.GetString() == "progress")
                                    {
                                        string stage = doc.RootElement.GetProperty("stage").GetString();
                                        float progress = (float)doc.RootElement.GetProperty("progress").GetDouble();
                                        GD.Print($"[SongImporter] Progress: {stage} {progress * 100:F0}%");
                                        CallDeferred(MethodName.EmitSignal, SignalName.ImportProgress, stage, progress);
                                    }
                                    else
                                    {
                                        // Non-progress JSON, log it
                                        CallDeferred(MethodName.EmitSignal, SignalName.ImportLog, $"[JSON] {line}");
                                    }
                                }
                                else
                                {
                                    // Human-readable log line
                                    string styledLine = line;
                                    // Basic anchor highlighting
                                    if (line.Contains("[1/5]")) styledLine = $"[color=yellow][SEP][/color] {line}";
                                    else if (line.Contains("[2/5]")) styledLine = $"[color=orange][ENH][/color] {line}";
                                    else if (line.Contains("[3/5]")) styledLine = $"[color=cyan][ASR][/color] {line}";
                                    else if (line.Contains("[4/5]")) styledLine = $"[color=pink][ALN][/color] {line}";
                                    else if (line.Contains("[5/5]")) styledLine = $"[color=magenta][PTH][/color] {line}";
                                    else if (line.Contains("[DONE]")) styledLine = $"[color=green][OK][/color] {line}";
                                    else if (line.Contains("[ERR]")) styledLine = $"[color=red][ERR][/color] {line}";
                                    
                                    GD.Print($"[SongImporter] Stderr: {line}");
                                    CallDeferred(MethodName.EmitSignal, SignalName.ImportLog, styledLine);
                                }
                            }
                            catch (Exception ex)
                            {
                                GD.PrintErr($"[SongImporter] Error parsing stderr line: {ex.Message}");
                                CallDeferred(MethodName.EmitSignal, SignalName.ImportLog, $"[color=orange]LOG: {line}[/color]");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"[SongImporter] Error reading stderr: {ex.Message}");
                    }
                });

                // Handle stdout (the final result)
                var outputTask = Task.Run(async () =>
                {
                    try
                    {
                        string line;
                        while ((line = await _currentProcess.StandardOutput.ReadLineAsync()) != null)
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                GD.Print($"[SongImporter] Stdout: {line}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"[SongImporter] Error reading stdout: {ex.Message}");
                    }
                });

                // Wait for both streams and process to complete
                await Task.WhenAll(errorTask, outputTask);
                _currentProcess.WaitForExit();

                GD.Print($"[SongImporter] Process exited with code: {_currentProcess.ExitCode}");

                if (_currentProcess.ExitCode != 0)
                {
                    string errorLog = string.Join("\n", errors.TakeLast(10)); // Last 10 error lines
                    throw new Exception($"Process failed with exit code {_currentProcess.ExitCode}:\n{errorLog}");
                }
            }
            finally
            {
                _currentProcess?.Dispose();
                _currentProcess = null;
            }
        }

        

        private List<string> QuoteArgs(string[] args)
        {
            var result = new List<string>();
            foreach (var arg in args)
            {
                if (arg.Contains(" ")) result.Add($"\"{arg}\"");
                else result.Add(arg);
            }
            return result;
        }
    }
}
