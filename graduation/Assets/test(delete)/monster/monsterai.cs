using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class monsterai : MonoBehaviour
{
    public NavMeshAgent navMesh;
    public GameObject target;
    // Start is called before the first frame update
    void Start()
    {
        navMesh= GetComponent<NavMeshAgent>();
    }

    // Update is called once per frame
    void Update()
    {
     navMesh.SetDestination(target.transform.position);   
    }
}
