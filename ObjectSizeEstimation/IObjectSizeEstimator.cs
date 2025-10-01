namespace ObjectSizeEstimation;

public interface IObjectSizeEstimator
{
    long EstimateSize(object instanceToEstimate);
}