using Godot;
using System;
using System.Collections.Generic;

namespace PitchGame
{
    public partial class SongList : ScrollContainer
    {
        [Export] public PackedScene SongItemScene { get; set; }
        
        [Signal] public delegate void SongSelectedEventHandler(SongData songData);
        
        private VBoxContainer _container;
        
        public override void _Ready()
        {
            _container = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            AddChild(_container);
            
            if (SongItemScene == null)
            {
                 // Try to load local scene or provide default
                SongItemScene = GD.Load<PackedScene>("res://scripts/Jukebox/SongListItem.tscn");
            }
            
            Refresh();
        }
        
        public void Refresh()
        {
            foreach(var child in _container.GetChildren()) child.QueueFree();
            
            string musicResPath = "res://Music";
            using var dir = DirAccess.Open(musicResPath);
            
            if (dir == null)
            {
                GD.PrintErr($"[SongList] Music directory not found: {musicResPath}");
                return;
            }
            
            dir.ListDirBegin();
            string subfolder = dir.GetNext();
            
            while (subfolder != "")
            {
                if (dir.CurrentIsDir() && !subfolder.StartsWith("."))
                {
                    var data = ValidateSongFolder($"{musicResPath}/{subfolder}");
                    if (data != null && data.IsValid)
                    {
                        var item = SongItemScene.Instantiate<SongListItem>();
                        item.Initialise(data);
                        item.Selected += (clickedItem) => OnItemSelected(clickedItem.Data);
                        _container.AddChild(item);
                    }
                }
                subfolder = dir.GetNext();
            }
        }
        
        private SongData ValidateSongFolder(string path)
        {
            var data = new SongData
            {
                Name = path.GetFile(),
                FolderPath = path,
                VocalsPath = $"{path}/vocals.wav",
                InstrumentalPath = $"{path}/instrumental.wav",
                KaraokeJsonPath = $"{path}/karaoke.json"
            };
            
            // Basic validation: all files must exist
            data.IsValid = FileAccess.FileExists(data.VocalsPath) && 
                           FileAccess.FileExists(data.InstrumentalPath) && 
                           FileAccess.FileExists(data.KaraokeJsonPath);
            
            return data;
        }
        
        private void OnItemSelected(SongData data)
        {
            EmitSignal(SignalName.SongSelected, data);
        }
    }
}