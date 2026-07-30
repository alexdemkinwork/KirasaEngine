using KirasaEngine.MGL.SceneGraph;
using KirasaEngine.MGL.Models;
using KirasaEngine.MGL.Instancing;
using System.Numerics;

namespace KirasaEngine.Editor.Views.Windows;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (DataContext as MainViewModel)!;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Button_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var scene = CreateDemoScene();
        scene.BackgroundColor = new Vector4(0.1f, 0.12f, 0.15f, 1f);
        scene.AmbientColor = new Vector3(0.15f, 0.15f, 0.15f);
        ViewModel.RenderFrameViewModel.UpdateScene(scene);
    }

    private static Scene CreateDemoScene()
    {
        var scene = new Scene
        {
            BackgroundColor = new Vector4(0.08f, 0.09f, 0.12f, 1f),
            AmbientColor = new Vector3(0.12f, 0.12f, 0.14f),
        };

        var cameraNode = new SceneNode
        {
            Name = "Camera",
            Camera = new Camera { FieldOfViewRadians = MathF.PI / 4f, NearPlane = 0.1f, FarPlane = 100f },
        };
        cameraNode.Transform.LocalPosition = new Vector3(6, 5, 10);
        var lookDirection = Vector3.Normalize(Vector3.Zero - cameraNode.Transform.LocalPosition);
        cameraNode.Transform.LocalRotation = RotationAligningLocalZ(-lookDirection);
        scene.Root.AddChild(cameraNode);

        var lightNode = new SceneNode
        {
            Name = "Light",
            Light = new Light { Type = LightType.Directional, Color = Vector3.One, Intensity = 1.2f },
        };
        var lightDirection = Vector3.Normalize(new Vector3(-0.4f, -1f, -0.3f));
        lightNode.Transform.LocalRotation = RotationAligningLocalZ(-lightDirection);
        scene.Root.AddChild(lightNode);

        var cubeMesh = MeshPrimitives.CreateCube(1f);
        var redMaterial = new Material { BaseColor = new Vector4(0.85f, 0.25f, 0.2f, 1f) };

        for (var x = -2; x <= 2; x++)
        for (var z = -2; z <= 2; z++)
        {
            var node = new SceneNode { Renderer = new MeshRenderer { Mesh = cubeMesh, Material = redMaterial } };
            node.Transform.LocalPosition = new Vector3(x * 1.5f, 0, z * 1.5f);
            scene.Root.AddChild(node);
        }

        var sphereMesh = MeshPrimitives.CreateSphere(0.5f, 20, 14);
        var sphereMaterial = new Material { BaseColor = Vector4.One };
        var batch = new InstancedBatch { Mesh = sphereMesh, Material = sphereMaterial };
        const int ringCount = 12;
        var ringInstances = new List<InstanceData>();
        for (var i = 0; i < ringCount; i++)
        {
            var angle = i / (float)ringCount * MathF.Tau;
            var position = new Vector3(MathF.Cos(angle) * 4.5f, 2.5f, MathF.Sin(angle) * 4.5f);
            var tint = new Vector4(HsvToRgb(i / (float)ringCount), 1f);
            ringInstances.Add(new InstanceData(Matrix4x4.CreateTranslation(position), tint));
        }
        batch.SetInstances(ringInstances);
        scene.InstancedBatches.Add(batch);

        return scene;
    }

    private static Quaternion RotationAligningLocalZ(Vector3 worldZ)
    {
        worldZ = Vector3.Normalize(worldZ);
        var up = MathF.Abs(Vector3.Dot(worldZ, Vector3.UnitY)) > 0.999f ? Vector3.UnitZ : Vector3.UnitY;
        var right = Vector3.Normalize(Vector3.Cross(up, worldZ));
        var actualUp = Vector3.Cross(worldZ, right);

        var m = new Matrix4x4(
            right.X, right.Y, right.Z, 0,
            actualUp.X, actualUp.Y, actualUp.Z, 0,
            worldZ.X, worldZ.Y, worldZ.Z, 0,
            0, 0, 0, 1);
        return Quaternion.CreateFromRotationMatrix(m);
    }

    private static Vector3 HsvToRgb(float hue)
    {
        var r = Math.Clamp(MathF.Abs(hue * 6 - 3) - 1, 0, 1);
        var g = Math.Clamp(2 - MathF.Abs(hue * 6 - 2), 0, 1);
        var b = Math.Clamp(2 - MathF.Abs(hue * 6 - 4), 0, 1);
        return new Vector3(r, g, b);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        ViewModel.RenderFrameViewModel.Dispose();
    }
}