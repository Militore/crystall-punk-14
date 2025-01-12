using Content.Server._CP14.MagicEnergy;
using Content.Server.Chat.Systems;
using Content.Shared._CP14.MagicEnergy.Components;
using Content.Shared._CP14.MagicSpell;
using Content.Shared._CP14.MagicSpell.Components;
using Content.Shared._CP14.MagicSpell.Events;
using Robust.Server.GameObjects;

namespace Content.Server._CP14.MagicSpell;

public sealed partial class CP14MagicSystem : CP14SharedMagicSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly CP14MagicEnergySystem _magicEnergy = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CP14MagicEffectVerbalAspectComponent, CP14VerbalAspectSpeechEvent>(OnSpellSpoken);

        SubscribeLocalEvent<CP14MagicEffectCastingVisualComponent, CP14StartCastMagicEffectEvent>(OnSpawnMagicVisualEffect);
        SubscribeLocalEvent<CP14MagicEffectCastingVisualComponent, CP14EndCastMagicEffectEvent>(OnDespawnMagicVisualEffect);

        SubscribeLocalEvent<CP14MagicEffectManaCostComponent, CP14MagicEffectConsumeResourceEvent>(OnManaConsume);
    }

    private void OnSpellSpoken(Entity<CP14MagicEffectVerbalAspectComponent> ent, ref CP14VerbalAspectSpeechEvent args)
    {
        if (args.Performer is not null && args.Speech is not null)
            _chat.TrySendInGameICMessage(args.Performer.Value, args.Speech, InGameICChatType.Speak, true);
    }

    private void OnSpawnMagicVisualEffect(Entity<CP14MagicEffectCastingVisualComponent> ent, ref CP14StartCastMagicEffectEvent args)
    {
        var vfx = SpawnAttachedTo(ent.Comp.Proto, Transform(args.Performer).Coordinates);
        _transform.SetParent(vfx, args.Performer);
        ent.Comp.SpawnedEntity = vfx;
    }

    private void OnDespawnMagicVisualEffect(Entity<CP14MagicEffectCastingVisualComponent> ent, ref CP14EndCastMagicEffectEvent args)
    {
        QueueDel(ent.Comp.SpawnedEntity);
        ent.Comp.SpawnedEntity = null;
    }

    private void OnManaConsume(Entity<CP14MagicEffectManaCostComponent> ent, ref CP14MagicEffectConsumeResourceEvent args)
    {
        if (!TryComp<CP14MagicEffectComponent>(ent, out var magicEffect))
            return;

        var requiredMana = CalculateManacost(ent, args.Performer);

        if (magicEffect.SpellStorage is not null &&
            TryComp<CP14MagicEnergyContainerComponent>(magicEffect.SpellStorage, out var magicStorage))
        {
            var spellEv = new CP14SpellFromSpellStorageUsedEvent(args.Performer, (ent, magicEffect), requiredMana);
            RaiseLocalEvent(magicEffect.SpellStorage.Value, ref spellEv);

            if (magicStorage.Energy > 0)
            {
                var cashedEnergy = magicStorage.Energy;
                if (_magicEnergy.TryConsumeEnergy(magicEffect.SpellStorage.Value, requiredMana, magicStorage, false))
                    requiredMana = MathF.Max(0, (float)(requiredMana - cashedEnergy));
            }
        }

        if (requiredMana > 0 &&
            TryComp<CP14MagicEnergyContainerComponent>(args.Performer, out var playerMana))
        {
            _magicEnergy.TryConsumeEnergy(args.Performer.Value, requiredMana, safe: false);
        }
    }
}
