namespace KirasaEngine.MGL.Models;

/// <summary>Factory helpers for common debug/demo meshes.</summary>
public static class MeshPrimitives
{
    public static Mesh CreateQuad(float size = 1f)
    {
        var h = size * 0.5f;
        var normal = new Vector3(0, 0, 1);
        var color = Vector4.One;
        VertexPNCT[] vertices =
        [
            new(new Vector3(-h, -h, 0), normal, color, new Vector2(0, 1)),
            new(new Vector3(h, -h, 0), normal, color, new Vector2(1, 1)),
            new(new Vector3(h, h, 0), normal, color, new Vector2(1, 0)),
            new(new Vector3(-h, h, 0), normal, color, new Vector2(0, 0)),
        ];
        uint[] indices = [0, 1, 2, 0, 2, 3];
        return new Mesh { Vertices = vertices, Indices = indices };
    }

    public static Mesh CreateCube(float size = 1f)
    {
        var h = size * 0.5f;
        var color = Vector4.One;

        (Vector3 normal, Vector3 u, Vector3 v)[] faces =
        [
            (new Vector3(0, 0, 1), new Vector3(1, 0, 0), new Vector3(0, 1, 0)),
            (new Vector3(0, 0, -1), new Vector3(-1, 0, 0), new Vector3(0, 1, 0)),
            (new Vector3(1, 0, 0), new Vector3(0, 0, -1), new Vector3(0, 1, 0)),
            (new Vector3(-1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 1, 0)),
            (new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, -1)),
            (new Vector3(0, -1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1)),
        ];

        var vertices = new VertexPNCT[faces.Length * 4];
        var indices = new uint[faces.Length * 6];

        for (var f = 0; f < faces.Length; f++)
        {
            var (normal, u, v) = faces[f];
            var center = normal * h;
            var vBase = (uint)(f * 4);

            vertices[vBase + 0] = new VertexPNCT(center - u * h - v * h, normal, color, new Vector2(0, 1));
            vertices[vBase + 1] = new VertexPNCT(center + u * h - v * h, normal, color, new Vector2(1, 1));
            vertices[vBase + 2] = new VertexPNCT(center + u * h + v * h, normal, color, new Vector2(1, 0));
            vertices[vBase + 3] = new VertexPNCT(center - u * h + v * h, normal, color, new Vector2(0, 0));

            var iBase = f * 6;
            indices[iBase + 0] = vBase + 0;
            indices[iBase + 1] = vBase + 1;
            indices[iBase + 2] = vBase + 2;
            indices[iBase + 3] = vBase + 0;
            indices[iBase + 4] = vBase + 2;
            indices[iBase + 5] = vBase + 3;
        }

        return new Mesh { Vertices = vertices, Indices = indices };
    }

    public static Mesh CreateSphere(float radius = 1f, int segments = 16, int rings = 12)
    {
        segments = Math.Max(segments, 3);
        rings = Math.Max(rings, 2);

        var vertices = new List<VertexPNCT>();
        var indices = new List<uint>();

        for (var r = 0; r <= rings; r++)
        {
            var v = (float)r / rings;
            var phi = v * MathF.PI;
            for (var s = 0; s <= segments; s++)
            {
                var u = (float)s / segments;
                var theta = u * MathF.Tau;

                var normal = new Vector3(
                    MathF.Sin(phi) * MathF.Cos(theta),
                    MathF.Cos(phi),
                    MathF.Sin(phi) * MathF.Sin(theta));

                vertices.Add(new VertexPNCT(normal * radius, normal, Vector4.One, new Vector2(u, v)));
            }
        }

        var rowStride = segments + 1;
        for (var r = 0; r < rings; r++)
        {
            for (var s = 0; s < segments; s++)
            {
                var a = (uint)(r * rowStride + s);
                var b = (uint)((r + 1) * rowStride + s);
                var c = (uint)((r + 1) * rowStride + s + 1);
                var d = (uint)(r * rowStride + s + 1);

                indices.Add(a); indices.Add(b); indices.Add(c);
                indices.Add(a); indices.Add(c); indices.Add(d);
            }
        }

        return new Mesh { Vertices = vertices.ToArray(), Indices = indices.ToArray() };
    }
}
