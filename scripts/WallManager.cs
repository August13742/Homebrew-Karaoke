using Godot;
using System;

namespace PitchGame
{
    public partial class WallManager : Node3D
    {
        [Export] public PackedScene WallScene;
        [Export] public float SpawnInterval = 3.5f;
        [Export] public float SpawnX = 30f;
        [Export] public int MinMidi = 48; // C3
        [Export] public int MaxMidi = 72; // C5
        
        // C Major scale notes
        private int[] _scale = { 48, 50, 52, 53, 55, 57, 59, 60, 62, 64, 65, 67, 69, 71, 72 };

        private float _timer = 0f;

        public override void _Ready()
        {
            _timer = SpawnInterval - 1.0f; // Spawn the first one soon
        }

        public override void _Process(double delta)
        {
            _timer += (float)delta;
            if (_timer >= SpawnInterval)
            {
                _timer = 0f;
                SpawnWall();
            }
        }

        private void SpawnWall()
        {
            if (WallScene == null) return;

            var wall = WallScene.Instantiate<PitchWall>();
            AddChild(wall);
            wall.Position = new Vector3(SpawnX, 0, 0);
            
            int randomMidi = _scale[GD.RandRange(0, _scale.Length - 1)];
            wall.Setup(randomMidi, -4f, 4f, MinMidi, MaxMidi);

            // Play vocal hint to help the player find the pitch
            if (AudioManager.Instance != null)
            {
                var hint = VocalSynthesiser.GenerateVocal(randomMidi, VocalSynthesiser.VowelType.A, 0.5f);
                AudioManager.Instance.PlaySFX(hint);
            }
        }
    }
}
