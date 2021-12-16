using UnityEngine;
public class Spin : MonoBehaviour 
{
    public float speed = 20f;
    void Update() => transform.Rotate( speed * new Vector3(2, 1, 0) * Time.deltaTime );
}