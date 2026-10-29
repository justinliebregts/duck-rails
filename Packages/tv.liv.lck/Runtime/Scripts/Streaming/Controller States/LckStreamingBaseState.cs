using System.Threading;
using System.Threading.Tasks;

namespace Liv.Lck.Streaming
{
    public abstract class LckStreamingBaseState
    {
        public abstract void EnterState(LckStreamingController controller);
    }
}
