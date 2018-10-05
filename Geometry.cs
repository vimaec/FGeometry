using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Ara3D
{
    public interface IElement<T> : IArray<int> where T : IElement<T>
    {
        IGeometry<T> Geometry { get; }
    }

    // An element is a generaliztion of a face / point / line /
    public struct Element<T> where T : IElement<T>
    {
        public int Index;
        public IGeometry<T> Geometry;
    }

    public interface IGeometry<T> : IMemoizer where T : IElement<T>
    {
        IArray<Vector3> Vertices { get; }
        IArray<T> Elements { get; }
    }

    public struct PolyFace : IElement<PolyFace>
    {
        public IGeometry<PolyFace> Geometry { get; }
        public PolyFace(IGeometry<PolyFace> g, IArray<int> indices)
        {
            Geometry = g;
            Indices = indices;
        }
        public int Count { get { return Indices.Count; } }
        public IArray<int> Indices { get; }
        public int this[int n] { get { return Indices[n]; } }
    }

    public struct QuadFace : IElement<QuadFace>
    {
        public IGeometry<QuadFace> Geometry { get; }
        public QuadFace(IGeometry<QuadFace> g, int a, int b, int c, int d)
        {
            Geometry = g;
            A = a; B = b; C = c; D = d;
        }
        public int A, B, C, D;
        public int Count { get { return 4; } }
        public int this[int n] { get { return n == 0 ? A : n == 1 ? B : n == 2 ? C : D; } }
    }

    public struct TriFace : IElement<TriFace>
    {
        public TriFace(IGeometry<TriFace> g, int a, int b, int c)
        {
            Geometry = g;
            A = a; B = b; C = c;
        }
        public int A, B, C;
        public IGeometry<TriFace> Geometry { get; }
        public int Count { get { return 3; } }
        public int this[int n] { get { return n == 0 ? A : n == 1 ? B : C; } }
    }

    public struct Line : IElement<Line>
    {
        public IGeometry<Line> Geometry { get; }
        public int A, B;
        public int Count { get { return 2; } }
        public int this[int n] { get { return n == 0 ? A : B; } }
    }

    public struct Point : IElement<Point>
    {
        public IGeometry<Point> Geometry { get; }
        public int Count { get { return 1; } }
        public int Index { get; }
        public int this[int n] { get { return Index; } }
    }

    public struct SphereElement : IElement<SphereElement>
    {
        public IGeometry<SphereElement> Geometry { get; }
        public int Count { get { return 1; } }
        public int Index { get; }
        public int this[int n] { get { return Index; } }
    }

    public struct BoxElement : IElement<BoxElement>
    {
        public IGeometry<BoxElement> Geometry { get; }
        public int Count { get { return 1; } }
        public int Index { get; }
        public int this[int n] { get { return Index; } }
    }

    public struct Edge<T> where T : IElement<T>
    {
        public Edge(T e, int i) { Element = e; Index = i; }
        public T Element { get; }
        public int Index { get; }        
    }

    public struct Corner<T> where T : IElement<T>
    {
        public T Element { get; }
        public int Index { get; }
    }

    public struct Vertex<T> where T : IElement<T>
    {
        public IGeometry<T> Geometry { get; }
        public int Index { get; }
    }

    public class BaseMesh: IGeometry<PolyFace>
    {
        public BaseMesh(IArray<Vector3> vertices, IArray<int> indices, IArray<int> faceIndices)
        {
            Vertices = vertices;
            Indices = indices;
            FaceIndices = faceIndices;
        }
        public IArray<int> Indices { get; }
        public IArray<int> FaceIndices { get; }
        public IArray<int> FaceCounts { get { return FaceIndices.Append(Indices.Count).AdjacentDifferences(); } }
        public IArray<Vector3> Vertices { get; }
        public IArray<PolyFace> Elements { get { return FaceIndices.Zip(FaceCounts, (i, c) => this.MakePolyFace(Indices.Subarray(i, c))); } }
        public ConcurrentDictionary<object, object> Cache { get; } = new ConcurrentDictionary<object, object>();
    }

    public class Polygon : BaseMesh
    {
        public Polygon(IArray<Vector3> vertices)
            : base(vertices, vertices.Indices(), 0.Repeat(1))
        { }
    }

    public class TriMesh : BaseMesh, IGeometry<TriFace> 
    {
        public TriMesh(IArray<Vector3> vertices, IArray<int> indices)
            : base(vertices, indices, indices.Indices().Stride(3))
        {
            Elements = (Indices.Count / 3).Select(i => this.MakeTriFace(Indices[i * 3], Indices[i * 3 + 1], Indices[i * 3 + 2]));
        }
        public new IArray<TriFace> Elements { get; }
    }

    public class QuadMesh : BaseMesh, IGeometry<QuadFace>
    {
        public QuadMesh(IArray<Vector3> vertices, IArray<int> indices)
            : base(vertices, indices, indices.Indices().Stride(3))
        {
            Elements = (Indices.Count / 4).Select(i => this.MakeQuadFace(Indices[i * 4], Indices[i * 4 + 1], Indices[i * 4 + 2], Indices[i * 4 + 3]));
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
        public static IArray<IArray<Edge<T>>> Edges<T>(this IGeometry<T> self) where T: IElement<T>
        {
            return self.Elements.Select(Edges);
        }

        public static IArray<Edge<T>> Edges<T>(this T self) where T : IElement<T>
        {
            return self.Select(i => new Edge<T>(self, i));
        }

        public static IEnumerable<U> Accumulate<T, U>(this IEnumerable<T> self, U init, Func<U, T, U> f) 
        {
            foreach (var x in self)
                yield return init = f(init, x);
        }

        public static IEnumerable<T> Accumulate<T>(this IEnumerable<T> self, Func<T, T, T> f)
        {
            return self.Accumulate(default(T), f);
        }

        public static IEnumerable<double> PartialSums(this IEnumerable<double> self)
        {
            return self.Accumulate((x, y) => x + y);
        }

        public static IEnumerable<int> PartialSums(this IEnumerable<int> self)
        {
            return self.Accumulate((x, y) => x + y);
        }

        public static IEnumerable<int> EnumerateIndices<T>(this IGeometry<T> g) where T: IElement<T>
        {
            return g.Elements.ToEnumerable().SelectMany(e => e.ToEnumerable());
        }

        public static IArray<int> UncachedIndices<T>(this IGeometry<T> g) where T: IElement<T>
        {
            return g.EnumerateIndices().ToIArray();
        }

        public static IArray<int> Indices<T>(this IGeometry<T> g) where T: IElement<T>
        {
            return g.Memoize(g.UncachedIndices);
        }

        public static TriFace MakeTriFace(this IGeometry<TriFace> self, int a, int b, int c)
        {
            return new TriFace(self, a, b, c);
        }

        public static QuadFace MakeQuadFace(this IGeometry<QuadFace> self, int a, int b, int c, int d)
        {
            return new QuadFace(self, a, b, c, d);
        }

        public static PolyFace MakePolyFace(this IGeometry<PolyFace> self, IArray<int> indices)
        {
            return new PolyFace(self, indices);
        }
    }
}
