using System;
using System.Collections.Generic;
using System.Text;

namespace Ara3D.Scenes
{
    public class Entity
    {
        public string Name;
        public Guid Id;
    }

    public class Material : Entity
    {
        public Vector4? Diffuse { get; }
        public Vector4? Specular { get; }
        public Vector4? Ambient { get; }
        public float Opacity { get; } = 1.0f;
        public float Transparency { get { return 1.0f - Opacity; } }
        public float? OpticalDenisty { get; }
    }

    public class Scene : Entity
    {
        IArray<Node> Nodes { get; }
    }

    public class Node : Entity
    {
        public Node Parent { get; }
        public Matrix4x4 WorldTransform { get; }
        public GeometricObject Geometry { get; }
    }

    public class GeometricObject : Entity
    {
        public IArray<Material> Materials { get; }
        public IArray<IGeometry> Geometries { get; }
    }
}
