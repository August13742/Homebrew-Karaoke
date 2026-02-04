using Godot;
using System;

namespace PitchGame
{
    public partial class MovementRangeIndicator : Node3D
    {
        [Export] public Ball BallNode;
        [Export] public float LineWidth = 0.05f;
        [Export] public Color LineColor = new Color(1, 1, 1, 0.5f);

        private MeshInstance3D _meshInstance;

        public override void _Ready()
        {
            if (BallNode == null)
            {
                GD.PrintErr("MovementRangeIndicator: BallNode is not assigned!");
                return;
            }

            CreateLine();
        }

        private void CreateLine()
        {
            float height = BallNode.MaxY - BallNode.MinY;
            float centerY = (BallNode.MaxY + BallNode.MinY) / 2f;

            _meshInstance = new MeshInstance3D();
            var mesh = new CylinderMesh();
            mesh.TopRadius = LineWidth;
            mesh.BottomRadius = LineWidth;
            mesh.Height = height;
            
            var material = new StandardMaterial3D();
            material.AlbedoColor = LineColor;
            material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            
            mesh.Material = material;
            _meshInstance.Mesh = mesh;

            AddChild(_meshInstance);

            // Position the line
            GlobalPosition = new Vector3(BallNode.GlobalPosition.X, centerY, BallNode.GlobalPosition.Z);
        }
    }
}
