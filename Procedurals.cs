namespace Ara3D
{
    public interface IProcedural<T>
    {
        Vector3 Eval(T x);
    }

    public interface IField : IProcedural<Vector3> {  }

    public interface ISurface : IProcedural<Vector2> { }

    public interface ICurve : IProcedural<float> {  }

}
