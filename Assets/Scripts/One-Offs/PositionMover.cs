using UnityEngine;

public class PositionMover : MonoBehaviour {
    public Transform[] destinations;
    public float speed = 1f;
    public bool loop = true;
    public Activatable callback;

    int currentDestination = 0;

    void Update() {
        if (currentDestination >= destinations.Length) {
            return;
        }
        Vector2.MoveTowards(this.transform.position, destinations[currentDestination].transform.position, speed * Time.deltaTime);
        if (this.transform.position.Equals(destinations[currentDestination].transform.position)) {
            currentDestination += 1;
            if (loop && currentDestination >= destinations.Length) {
                currentDestination = 0;
            }
            if (callback != null) {
                callback.Activate();
            }
        }
    }    
}