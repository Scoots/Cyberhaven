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

        private readonly IJoinManager _joinManager;
        private readonly IStatsMap _statsMap;

        public CyberhavenController(IJoinManager joinManager, IStatsMap statsMap)
        {
            _joinManager = joinManager;
            _statsMap = statsMap;
        }

        [HttpPost]
        [Route("/join")]
        public async Task<ActionResult<JoinResponse>> JoinWithOtherClient()
        {
            TaskCompletionSource<JoinResponse> taskCompletionSource = new();
            _joinManager.Enqueue(new JoinRequestQueueMessage
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
            // Wasteful in the name of safety, converting from Dict to ReadOnlyDict back to Dict
            return _statsMap.Stats.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }
}
