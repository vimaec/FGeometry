using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Ara3D
{
    public interface IElement : IArray<int> 
    {
        IGeometry Geometry { get; }
    }

    // An element is a generaliztion of a face / point / line /
    public struct Element<T> where T : IElement
    {
        public int Index;
        public IGeometry Geometry;
    }

    public interface IGeometry : IMemoizer 
    {
        IArray<Vector3> Vertices { get; }
        IArray<IElement> Elements { get; }
    }

    public struct PolyFace : IElement
    {
        public IGeometry Geometry { get; }
        public PolyFace(IGeometry g, IArray<int> indices)
        {
            Geometry = g;
            Indices = indices;
        }
        public int Count => Indices.Count;
        public IArray<int> Indices { get; }
        public int this[int n] { get { return Indices[n]; } }
    }

    public struct QuadFace : IElement
    {
        public IGeometry Geometry { get; }
        public QuadFace(IGeometry g, int a, int b, int c, int d)
        {
            Geometry = g;
            A = a; B = b; C = c; D = d;
        }
        public int A, B, C, D;
        public int Count => 4;
        public int this[int n] { get { return n == 0 ? A : n == 1 ? B : n == 2 ? C : D; } }
    }

    public struct TriFace : IElement
    {
        public TriFace(IGeometry g, int a, int b, int c)
        {
            Geometry = g;
            A = a; B = b; C = c;
        }
        public int A, B, C;
        public IGeometry Geometry { get; }
        public int Count => 3;
        public int this[int n] { get { return n == 0 ? A : n == 1 ? B : C; } }
    }

    public struct Line : IElement
    {
        public IGeometry Geometry { get; }
        public int A, B;
        public int Count => 2;
        public int this[int n] { get { return n == 0 ? A : B; } }
    }

    public struct Point : IElement
    {
        public IGeometry Geometry { get; }
        public int Count => 1;
        public int Index { get; }
        public int this[int n] { get { return Index; } }
    }

    public struct SphereElement : IElement
    {
        public IGeometry Geometry { get; }
        public int Count => 1;
        public int Index { get; }
        public int this[int n] { get { return Index; } }
    }

    public struct BoxElement : IElement
    {
        public IGeometry Geometry { get; }
        public int Count => 1;
        public int Index { get; }
        public int this[int n] { get { return Index; } }
    }

    public struct Edge 
    {
        public Edge(IElement e, int i) { Element = e; Index = i; }
        public IElement Element { get; }
        public int Index { get; }        
    }

    public struct Corner
    {
        public IElement Element { get; }
        public int Index { get; }
    }

    public struct Vertex
    {
        public IGeometry Geometry { get; }
        public int Index { get; }
    }

    public class BaseMesh: IGeometry
    {
        public BaseMesh(IArray<Vector3> vertices, IArray<int> indices, IArray<int> faceIndices)
        {
            Vertices = vertices;
            Indices = indices;
            FaceIndices = faceIndices;
        }
        public IArray<int> Indices { get; }
        public IArray<int> FaceIndices { get; }
        public IArray<int> FaceCounts => FaceIndices.Append(Indices.Count).AdjacentDifferences();
        public IArray<Vector3> Vertices { get; }
        public IArray<IElement> Elements => FaceIndices.Zip(FaceCounts, (i, c) => this.PolyFace(Indices.Subarray(i, c)) as IElement);
        public ConcurrentDictionary<object, object> Cache { get; } = new ConcurrentDictionary<object, object>();
    }

    public class Polygon : BaseMesh
    {
        public Polygon(IArray<Vector3> vertices)
            : base(vertices, vertices.Indices(), 0.Repeat(1))
        { }
    }

    public class TriMesh : BaseMesh, IGeometry 
    {
        public TriMesh(IArray<Vector3> vertices, IArray<int> indices)
            : base(vertices, indices, indices.Indices().Stride(3))
        {
            Elements = (Indices.Count / 3).Select(i => this.TriFace(Indices[i * 3], Indices[i * 3 + 1], Indices[i * 3 + 2]));
        }
        public new IArray<TriFace> Elements { get; }
    }

    public class QuadMesh : BaseMesh, IGeometry
    {
        public QuadMesh(IArray<Vector3> vertices, IArray<int> indices)
            : base(vertices, indices, indices.Indices().Stride(4))
        {
            Elements = (Indices.Count / 4).Select(i => this.QuadFace(Indices[i * 4], Indices[i * 4 + 1], Indices[i * 4 + 2], Indices[i * 4 + 3]));
        }
        public new IArray<QuadFace> Elements { get; }
    }

    /*
    public class PolyMesh : Geometry<PolyFace> { }
    public class TriMesh : Geometry<TriFace> { }
    public class QuadMesh : Geometry<QuadFace> { }
    public class Lines : Geometry<Line> { }
    public class Points : Geometry<Line> { }
    public class Spheres : Geometry<SphereElement> { }
    public class Boxes : Geometry<BoxElement> { }
    */

    public class TriMeshBuilder
    {
        List<Vector3> vertexBuffer = new List<Vector3>();
        List<int> indexBuffer = new List<int>();

        public TriMeshBuilder Add(Vector3 v)
        {
            vertexBuffer.Add(v);
            return this;
        }
        public TriMeshBuilder AddFace(int a, int b, int c)
        {
            indexBuffer.Add(a);
            indexBuffer.Add(b);
            indexBuffer.Add(c);
            return this;
        }

        public TriMesh ToMesh()
        {            
            var obj = new object();
            lock (obj) {
                var r = new TriMesh(vertexBuffer.ToIArray(), indexBuffer.ToIArray());
                vertexBuffer = null;
                indexBuffer = null;
                return r;
            }
        }
    }

    public static class Geometry
    {
        public static IArray<IArray<Edge>> Edges(this IGeometry self)
        {
            return self.Elements.Select(Edges);
        }

        public static IArray<Edge> Edges(this IElement self) 
        {
            return self.Select(i => new Edge(self, i));
        }

        public static IEnumerable<int> EnumerateIndices(this IGeometry g)
        {
            return g.Elements.ToEnumerable().SelectMany(e => e.ToEnumerable());
        }

        public static IArray<int> UncachedIndices(this IGeometry g)
        {
            return g.EnumerateIndices().ToIArray();
        }

        public static IArray<int> Indices(this IGeometry g)
        {
            return g.Memoize(g.UncachedIndices);
        }

        public static TriFace TriFace(this IGeometry self, int a, int b, int c)
        {
            return new TriFace(self, a, b, c);
        }

        public static QuadFace QuadFace(this IGeometry self, int a, int b, int c, int d)
        {
            return new QuadFace(self, a, b, c, d);
        }

        public static PolyFace PolyFace(this IGeometry self, IArray<int> indices)
        {
            return new PolyFace(self, indices);
        }
    }
}
