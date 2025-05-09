public class QuantumCombatTest : MonoBehaviour
{
    [SerializeField] float phaseTransitionDuration = 1.5f;
    
    void Update() {
        if (Input.GetKeyDown(KeyCode.Q)) {
            StartCoroutine(PhaseDimensionTransition());
        }
    }
    
    IEnumerator PhaseDimensionTransition() {
        Physics.IgnoreLayerCollision(3, 7, true);
        yield return new WaitForSeconds(phaseTransitionDuration);
        Physics.IgnoreLayerCollision(3, 7, false);
    }
}