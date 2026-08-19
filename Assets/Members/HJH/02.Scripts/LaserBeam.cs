using FORGE3D;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
public class LaserBeam : MonoBehaviour
{
    public float laserAttackPower = 10f;
    [SerializeField] private float laserDuration = 5f;
    public float LaserDuration => laserDuration;

    [SerializeField] private float damageInterval = 0.2f;
    [SerializeField] private float range = 30f;
    public GameObject impactParticle;
    public LayerMask enemyMask;

    private Transform _firePoint;
    private float _timer;
    private float _damageTimer;
    private F3DLightning _lightning;

    public void SetFirePoint(Transform firePoint) => _firePoint = firePoint;

    private void Start()
    {
        if (impactParticle != null)
        {
            _lightning = impactParticle.GetComponent<F3DLightning>();
            impactParticle.SetActive(true);
            impactParticle.BroadcastMessage("OnSpawned", SendMessageOptions.DontRequireReceiver);
        }
    }

    private void Update()
    {
        FollowFirePoint();

        _timer += Time.deltaTime;
        _damageTimer += Time.deltaTime;
        if (_damageTimer >= damageInterval)
        {
            _damageTimer -= damageInterval;
        }

        if (_timer >= laserDuration)
        {
            if (impactParticle != null)
            {
                impactParticle.BroadcastMessage("OnDespawned", SendMessageOptions.DontRequireReceiver);
                impactParticle.SetActive(false);
            }
            Destroy(gameObject);
        }
    }

    private void FollowFirePoint()
    {
        if (_firePoint == null) return;

        transform.position = _firePoint.position;
        transform.rotation = _firePoint.rotation;

        if (impactParticle != null)
        {
            if (Physics.Raycast(_firePoint.position, _firePoint.forward, out RaycastHit hit, range, enemyMask))
            {
                impactParticle.transform.position = hit.point;
                impactParticle.transform.rotation = Quaternion.LookRotation(hit.normal);
            }
            else
            {
                impactParticle.transform.position = _firePoint.position + _firePoint.forward * range;
                impactParticle.transform.rotation = _firePoint.rotation;
            }
        }
    }
}