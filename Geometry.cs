using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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

    public struct Edge : IArray<Vector3>
    {
        public Edge(IElement e, int i) { Element = e; Index = i; }
        public IElement Element { get; }
        public int Index { get; }       
        public int Count { get { return 2; } }
        public Vector3 this[int n] { get { return Element.Points().ElementAtModulo(Index + n); } }
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

    public class TriMesh : BaseMesh
    {
        public TriMesh(IArray<Vector3> vertices, IArray<int> indices)
            : base(vertices, indices, indices.Indices().Stride(3))
        {
            Elements = (Indices.Count / 3).Select(i => this.TriFace(Indices[i * 3], Indices[i * 3 + 1], Indices[i * 3 + 2]));
        }
        public new IArray<TriFace> Elements { get; }
    }

    public class QuadMesh : BaseMesh
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

    public static class ArrayHelpers
    {
        public static Vector3 Sum(this IArray<Vector3> self)
        {
            return self.Aggregate(Vector3.Zero, (a, b) => a + b);
        }

        public static Vector3 Average(this IArray<Vector3> self)
        {
            return self.Sum() / self.Count;
        }
    }

    public static class Geometry
    {
        // Epsilon is bigger than the real epsilon. 
        public const float EPSILON = float.Epsilon * 100;

        public static IArray<IArray<Edge>> Edges(this IGeometry self)
        {
            return self.Elements.Select(Edges);
        }

        public static IArray<Edge> Edges(this IElement self) 
        {
            return self.Select(i => new Edge(self, i));
        }

        public static Vector3 MidPoint(this IElement self)
        {
            return self.Points().Average();
        }

        public static IArray<Vector3> EdgeMidPoints(this IElement self)
        {
            return self.Edges().Select(e => e.Average());
        }

        // TODO: this could be broken.
        public static IArray<Vector3> Points(this IElement self)
        {
            return self.Geometry.Vertices.SelectByIndex(self);
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

        public static int FaceCount(this IGeometry self)
        {
            return self.Elements.Count;
        }

        public static IGeometry ToPolyMesh(this IGeometry self, IEnumerable<IEnumerable<int>> indices)
        {
            var verts = self.Vertices;
            var flatIndices = indices.SelectMany(xs => xs).ToIArray();
            var faceIndices = indices.Where(xs => xs.Any()).Select(xs => xs.First()).ToIArray();
            return new BaseMesh(verts, flatIndices, faceIndices);
        }  

        public static IGeometry MergeCoplanar(this IGeometry self)
        {
            if (self.Elements.Count <= 1) return self;
            var curPoly = new List<int>();
            var polys = new List<List<int>> { curPoly };
            var cur = 0;
            for (var i=1; i < self.Elements.Count; ++i)
            {
                if (!self.CanMergeTris(cur, i))
                {
                    cur = i;
                    polys.Add(curPoly = new List<int>());
                }
                curPoly.Add(self.Elements[i].ToList());
            }
            return self.ToPolyMesh(polys);
        }        

        public static Vector3 Tangent(this IElement self)
        {
            return self.Points()[1] - self.Points()[0];
        }

        public static Vector3 Binormal(this IElement self)
        {
            return self.Points()[2] - self.Points()[0];
        }

        public static bool Coplanar(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4, float epsilon = EPSILON)
        {
            // https://en.wikipedia.org/wiki/Coplanarity
            return Math.Abs(Vector3.Dot(v3 - v1, Vector3.Cross(v2 - v1, v4 - v1))) < epsilon;
        }

        public static Vector3 Normal(this IElement self)
        {
            return Vector3.Normalize(Vector3.Cross(self.Binormal(), self.Tangent()));
        }

        public static IGeometry ToQuadMesh(Func<Vector2, Vector3> f, int usegs, int vsegs)
        {
            var verts = new List<Vector3>();
            var indices = new List<int>();
            for (var i = 0; i <= usegs; ++i)
            {
                var u = (float)i / usegs;
                for (var j = 0; j <= vsegs; ++j)
                {
                    var v = (float)j / vsegs;
                    verts.Add(f(new Vector2(u, v)));

                    if (i < usegs && j < vsegs)
                    {
                        indices.Add(i * (vsegs + 1) + j);
                        indices.Add(i * (vsegs + 1) + j + 1);
                        indices.Add((i + 1) * (vsegs + 1) + j + 1);
                        indices.Add((i + 1) * (vsegs + 1) + j);
                    }
                }
            }
            return new QuadMesh(verts.ToIArray(), indices.ToIArray());
        }
        public static bool CanMergeTris(this IGeometry self, int a, int b)
        {
            var e1 = self.Elements[a];
            var e2 = self.Elements[b];
            if (e1.Count != e2.Count && e1.Count != 3) return false;
            var indices = new[] { e1[0], e1[1], e1[2], e2[0], e2[1], e2[2] }.Distinct().ToIArray();
            if (indices.Count != 4) return false;
            var verts = self.Vertices.SelectByIndex(indices);
            return Coplanar(verts[0], verts[1], verts[2], verts[3]);
        }
        public static IEnumerable<Vector3> UsedVertices(this IGeometry self)
        {
            return self.Elements.ToEnumerable().SelectMany(es => es.Points().ToEnumerable());
        }
        public static IArray<Vector3> FaceMidPoints(this IGeometry self)
        {
            return self.Elements.Select(e => e.MidPoint());
        }
    }
}
