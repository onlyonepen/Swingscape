using Script.Enemy;
using Script.Enemy.State;

public interface IEnemyStateFactory
{
    EnemyBaseState CreateIdleState(BaseEnemy enemy);
    EnemyBaseState CreateAggroState(BaseEnemy enemy);
    EnemyBaseState CreateAttackState(BaseEnemy enemy);
    EnemyBaseState CreateStaggerState(BaseEnemy enemy);
    EnemyBaseState CreateDeadState(BaseEnemy enemy);
}