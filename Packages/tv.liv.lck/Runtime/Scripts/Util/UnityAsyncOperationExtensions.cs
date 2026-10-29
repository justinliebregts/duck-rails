using System.Threading.Tasks;
using UnityEngine;

namespace Liv.Lck
{
    public static class UnityAsyncOperationExtensions
    {
        public static Task AsTask(this AsyncOperation op)
        {
            var tcs = new TaskCompletionSource<object>();
            op.completed += _ => tcs.SetResult(null);
            return tcs.Task;
        }
    }
}