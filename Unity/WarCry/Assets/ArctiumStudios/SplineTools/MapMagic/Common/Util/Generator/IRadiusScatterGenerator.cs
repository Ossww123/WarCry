#if ST_MM_1 || ST_MM_2

namespace ArctiumStudios.SplineTools
{
    public interface IRadiusScatterGenerator : GraphGenerator
    {
        float OuterMargin();

        string GetAroundType();
    }
}

#endif