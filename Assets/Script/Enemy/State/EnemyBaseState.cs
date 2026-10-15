using System;
using UnityEngine;

namespace Script.Enemy.State
{
    public abstract class EnemyBaseState
    {
        protected BaseEnemy Enemy;
        protected float StateEnterTime;
        

        public virtual void OnStateEnter()
        {
            StateEnterTime = Time.time;
        }
        public virtual void OnStateUpdate() {  }
        public virtual void OnStateExit() {  }

        /// <summary>Whether BaseEnemy's idle hover bob (HeavyDrone only) should run while this state is active.</summary>
        public virtual bool AllowsBob => true;
    }
}