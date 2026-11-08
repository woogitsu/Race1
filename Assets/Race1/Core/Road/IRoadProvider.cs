namespace Race1.Core
{
    /// <summary>Anything that can describe the road at an arc length. Implemented by RoadTrack and test roads.</summary>
    public interface IRoadProvider
    {
        RoadSample Sample(double s);
    }
}
