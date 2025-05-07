namespace text_survival.Effects;

interface IEffect
{
    void Apply();
    void Update();
    void Remove();
    bool IsActive {get;}
    
}