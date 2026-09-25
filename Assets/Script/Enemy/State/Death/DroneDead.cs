using UnityEngine;

namespace Script.Enemy.State.Death
{
    // Terminal state entered the moment an enemy is sliced/killed. Unlike Stagger (which times
    // out and hands control back to Aggro/Idle), this never transitions out on its own, so a
    // dying enemy can't resume attacking while its death/split is still playing out.
    public class DroneDead : EnemyBaseState
    {
        public DroneDead(BaseEnemy enemy)
        {
            Enemy = enemy;
        }

        public override void OnStateEnter()
        {
            base.OnStateEnter();
            Enemy.rb.constraints = RigidbodyConstraints.FreezeRotation;
            Enemy.PlayDeathParticles();
        }
    }
}
