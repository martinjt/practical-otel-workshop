using System.Collections.Immutable;
using OpenTelemetry.Trace;

public class HealthCheckSampler<T> : Sampler where T : Sampler
{
    private static Random _random = new();
    private readonly int _keepPercentage;
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly T _innerSampler;

    public HealthCheckSampler(int keepPercentage, IHttpContextAccessor contextAccessor, T innerSampler)
    {
        _keepPercentage = keepPercentage;
        _contextAccessor = contextAccessor;
        _innerSampler = innerSampler;
    }

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {

        if (_contextAccessor.HttpContext?.Request.Path == "/health")
        {
            var shouldSample = _random.Next(1, 100) < _keepPercentage;
            if (shouldSample)
            {
                var samplingAttributes = ImmutableList.CreateBuilder<KeyValuePair<string, object>>();
                samplingAttributes.Add(new("SampleRate", _keepPercentage));

                return new SamplingResult(SamplingDecision.RecordAndSample, samplingAttributes.ToImmutableList());          
            }
                
            return new SamplingResult(SamplingDecision.Drop);
        }

        return _innerSampler.ShouldSample(samplingParameters);
    }
}