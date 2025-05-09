public class AsyncRPCManager : MonoBehaviour
{
    public IEnumerator PerformRemoteAction(string actionId)
    {
        yield return StartCoroutine(NetworkService.SendRPC(actionId));
        UpdateLocalState(actionId);
    }
}