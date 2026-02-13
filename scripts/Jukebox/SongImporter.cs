using Godot;
using System;
using System.Collections.Generic;
using System.IO;
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

            GD.Print($"[SongImporter] Importing {fileName} to {outputDir}");
            EmitSignal(SignalName.ImportStarted, fileName);

            try
            {
                // Prepare arguments for 'uv run main.py karaoke'
                string pythonRoot = ProjectSettings.GlobalizePath("res://Audio-Processing-Utilities");
                string mainPy = Path.Combine(pythonRoot, "main.py");
                
                string[] args = { 
                    "run", 
                    mainPy, 
                    "karaoke", 
                    inputPath, 
                    outputDir 
                };

                // We use uv from PATH. 
                // Note: On Linux, 'uv' should be in the shell environment.
                
                int pid = OS.CreateProcess("uv", args, true); // true to open child's stdout/stderr pipes? 
                // Actually OS.CreateProcess doesn't return pipes easily in C# Godot 4.
                // Better to use System.Diagnostics.Process for fine-grained IO control.
                
                await RunProcessWithOutput("uv", args, pythonRoot);
                
                EmitSignal(SignalName.ImportCompleted, songName, true);
            }
            catch (Exception e)
            {
                GD.PrintErr($"[SongImporter] Error during import: {e.Message}");
                EmitSignal(SignalName.ImportLog, $"Exception: {e.Message}");
                EmitSignal(SignalName.ImportCompleted, songName, false);
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

            using (var process = new System.Diagnostics.Process { StartInfo = startInfo })
            {
                process.Start();

                // Handle stderr for progress
                var errorTask = Task.Run(async () =>
                {
                    while (!process.StandardError.EndOfStream)
                    {
                        var line = await process.StandardError.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        try
                        {
                            if (line.StartsWith("{"))
                            {
                                var doc = JsonDocument.Parse(line);
                                if (doc.RootElement.TryGetProperty("type", out var type) && type.GetString() == "progress")
                                {
                                    string stage = doc.RootElement.GetProperty("stage").GetString();
                                    float progress = (float)doc.RootElement.GetProperty("progress").GetDouble();
                                    CallDeferred(MethodName.EmitSignal, SignalName.ImportProgress, stage, progress);
                                }
                            }
                            else
                            {
                                CallDeferred(MethodName.EmitSignal, SignalName.ImportLog, line);
                            }
                        }
                        catch
                        {
                            CallDeferred(MethodName.EmitSignal, SignalName.ImportLog, line);
                        }
                    }
                });

                // Handle stdout (the final result)
                var outputTask = Task.Run(async () =>
                {
                    while (!process.StandardOutput.EndOfStream)
                    {
                        var line = await process.StandardOutput.ReadLineAsync();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                             GD.Print($"[Python STDOUT] {line}");
                        }
                    }
                });

                await Task.WhenAll(errorTask, outputTask);
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"Process failed with exit code {process.ExitCode}");
                }
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
