namespace Survival;

internal enum IntakeKind { Grazed, Carcass, Remains, Store }

internal interface IIntakeSink
{
    void Took(Agent animal, IntakeKind kind, double kcal, Cause? killedBy);
}
