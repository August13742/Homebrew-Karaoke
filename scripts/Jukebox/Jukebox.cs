using Godot;
using System;
using AudioSystem;

namespace PitchGame
{
    public partial class Jukebox : Control
    {
        [Export] public string KaraokeScenePath = "res://scenes/KaraokeScene.tscn";

        private SongList _songList;
        private SongInspector _songInspector;
        private Button _btnBack;
        
        private SongImporter _songImporter;
        private ImportStatusPanel _importStatusPanel;
        
        public override void _Ready()
        {
            _songList = GetNode<SongList>("%SongList");
            _songInspector = GetNode<SongInspector>("%SongInspector");
            _btnBack = GetNode<Button>("%BtnBack");
            
            _songList.SongSelected += (data) => 
            {
                _songInspector.Inspect(data);
                PlaySongPreview(data);
            };

            _songInspector.StartRequested += (data, keyShift) =>
            {
                StartKaraoke(data, keyShift);
            };
            
            _btnBack.Pressed += OnBackInternal;

            SetupImporter();
        }

        private void SetupImporter()
        {
            // Add SongImporter logic node
            _songImporter = new SongImporter();
            _songImporter.Name = "SongImporter";
            AddChild(_songImporter);

            // Instantiate UI
            var panelScene = GD.Load<PackedScene>("res://scenes/components/ImportStatusPanel.tscn");
            _importStatusPanel = panelScene.Instantiate<ImportStatusPanel>();
            AddChild(_importStatusPanel);

            // Wire signals
            _songImporter.ImportStarted += (fileName) => _importStatusPanel.Start(fileName);
            _songImporter.ImportProgress += (stage, progress) => _importStatusPanel.UpdateProgress(stage, progress);
            _songImporter.ImportLog += (msg) => _importStatusPanel.AddLog(msg);
            _songImporter.ImportCompleted += (name, success) => 
            {
                _importStatusPanel.Complete(name, success);
                if (success) _songList.Refresh();
            };

            // Hook into OS file drop
            GetTree().Root.FilesDropped += (files) => _songImporter.ProcessFiles(files);
        }
        
        private void PlaySongPreview(SongData data)
        {
            if (data == null || string.IsNullOrEmpty(data.InstrumentalPath)) return;

            var musicRes = new MusicResource
            {
                Clip = ExternalAudioLoader.LoadAudio(data.InstrumentalPath),
                VocalClip = ExternalAudioLoader.LoadAudio(data.VocalsPath),
                Volume = 1.0f,
                FadeTime = 1.0f,
                Loop = true
            };
        
            AudioManager.Instance?.PlayMusic(musicRes);
        }

        private void StartKaraoke(SongData data, float keyShift)
        {
            // Store in SessionData for the next scene
            SessionData.CurrentSong = data;
            SessionData.KeyShift = keyShift;

            // Transition to KaraokeScene
            // Using CrossfadeManager if it exists, otherwise standard scene change
            var crossfade = GetTree().Root.FindChild("CrossfadeManager", true, false);
            if (crossfade != null)
            {
                crossfade.Call("LoadScene", KaraokeScenePath, 0.5f);
            }
            else
            {
                GetTree().ChangeSceneToFile(KaraokeScenePath);
            }
        }

        private void OnBackInternal()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopMusic();
                AudioManager.Instance.SetMusicPitchShift(0f);
            }
            SessionData.KeyShift = 0f;
            
            // Return to MainMenu or similar
            GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
        }

    }
}