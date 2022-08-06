using Cyberhaven.Data;
using Microsoft.AspNetCore.Mvc;

namespace Cyberhaven.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CyberhavenController : ControllerBase
    {
        // This is just because we don't have access to user information without the appropriate
        // authentication/authorization hooks
        private static int fakeUserId = 0;

        [HttpPost]
        [Route("/join")]
        public async Task<ActionResult<JoinResponse>> JoinWithOtherClient()
        {
            TaskCompletionSource<JoinResponse> taskCompletionSource = new();
            ClientJoinManager.Instance.Enqueue(new ApiQueueMessage
            {
                UserId = fakeUserId++,
                TaskSource = taskCompletionSource
            });

            return await taskCompletionSource.Task;
        }

        [HttpGet]
        [Route("/stats")]
        public ActionResult<Dictionary<string, long>> GetStats()
        {
            return ClientJoinManager.Instance.StatsMap;
        }
    }
}
