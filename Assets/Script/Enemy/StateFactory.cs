using System;
using Script.Enemy.State;
using Script.Enemy.State.Aggro;
using Script.Enemy.State.Attack;
using Script.Enemy.State.Death;
using Script.Enemy.State.GetPull;
using Script.Enemy.State.Idle;
using UnityEngine;

namespace Script.Enemy
{
    /// <summary>
    /// Shared base for all drone-family enemies.
    /// Idle, Aggro, and Stagger are identical across variants — only Attack differs.
    /// A new drone type only needs to override CreateAttackState.
    /// </summary>
    public abstract class DroneFactoryBase : IEnemyStateFactory
    {
        public EnemyBaseState CreateIdleState(BaseEnemy enemy)    => new DroneIdle(enemy);
        public EnemyBaseState CreateAggroState(BaseEnemy enemy)   => new DroneAggro(enemy);
        public EnemyBaseState CreateStaggerState(BaseEnemy enemy) => new DroneStagger(enemy);
        public EnemyBaseState CreateDeadState(BaseEnemy enemy)    => new DroneDead(enemy);
        public abstract EnemyBaseState CreateAttackState(BaseEnemy enemy);
    }

    public class LightDroneFactory : DroneFactoryBase
    {
        public override EnemyBaseState CreateAttackState(BaseEnemy enemy) => new DroneAttack(enemy);
    }

    public class HeavyDroneFactory : DroneFactoryBase
    {
        public override EnemyBaseState CreateAttackState(BaseEnemy enemy) => new DroneSpreadShot(enemy);
    }
}