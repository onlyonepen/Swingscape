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

        public override bool AllowsBob => false;

        public override void OnStateEnter()
        {
            base.OnStateEnter();

            // Stop the rig's own idle animations (e.g. wing flaps) so it doesn't keep moving on
            // its own while the corpse is falling - physics should be the only thing driving it now.
            foreach (var motor in Enemy.GetComponentsInChildren<SimpleTransformMotor>())
            {
                motor.enabled = false;
            }

            // The actual break-into-pieces + teardown happens later, in BaseEnemy.Death() -
            // it has to wait until the async slice finishes and calls back into this enemy, so
            // nothing here can detach/destroy anything the callback still needs.
            Enemy.rigBuilder.enabled = false;
            Enemy.PlayDeathParticles();
        }
    }
}
