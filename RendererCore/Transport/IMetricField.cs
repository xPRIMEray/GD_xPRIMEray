using System.Numerics;
using RendererCore.SceneSnapshot;

namespace RendererCore.Transport;

public interface IMetricField
{
    Vector3 AccelAt(Vector3 pWorld, in RendererCore.SceneSnapshot.SceneSnapshot snapshot);
}
