using Members.JJH._02_Scripts.Agents.Modules;
using UnityEngine;

namespace Members.KYR._01.Scripts.Modules
{
    public class PlayerSensor : AgentSensor
    {
        public override bool IsTargetInSight(float range, float sight)
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, range, TargetLayer);

            foreach (Collider collider in colliders)
            {
                Vector3 direction = collider.transform.position - transform.position;
                float angle = Vector3.Angle(transform.forward, direction);

                if (angle <= sight)
                    return true;
            }

            return false;
        }
    }
}