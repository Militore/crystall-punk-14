/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Shared._CP14.Knowledge;
using Content.Shared._CP14.Workbench;
using Content.Shared._CP14.Workbench.Prototypes;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._CP14.Workbench;

public sealed partial class CP14WorkbenchSystem : SharedCP14WorkbenchSystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedCP14KnowledgeSystem _knowledge = default!;

    private EntityQuery<MetaDataComponent> _metaQuery;
    private EntityQuery<StackComponent> _stackQuery;

    public override void Initialize()
    {
        base.Initialize();

        _metaQuery = GetEntityQuery<MetaDataComponent>();
        _stackQuery = GetEntityQuery<StackComponent>();

        SubscribeLocalEvent<CP14WorkbenchComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<CP14WorkbenchComponent, BeforeActivatableUIOpenEvent>(OnBeforeUIOpen);
        SubscribeLocalEvent<CP14WorkbenchComponent, CP14WorkbenchUiCraftMessage>(OnCraft);

        SubscribeLocalEvent<CP14WorkbenchComponent, CP14CraftDoAfterEvent>(OnCraftFinished);
    }

    private void OnMapInit(Entity<CP14WorkbenchComponent> ent, ref MapInitEvent args)
    {
        foreach (var recipe in _proto.EnumeratePrototypes<CP14WorkbenchRecipePrototype>())
        {
            if (ent.Comp.Recipes.Contains(recipe))
                continue;

            if (!ent.Comp.RecipeTags.Contains(recipe.Tag))
                continue;

            ent.Comp.Recipes.Add(recipe);
        }
    }

    private void OnBeforeUIOpen(Entity<CP14WorkbenchComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        UpdateUIRecipes(ent, args.User);
    }

    private void OnCraftFinished(Entity<CP14WorkbenchComponent> ent, ref CP14CraftDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (!_proto.TryIndex(args.Recipe, out var recipe))
            return;

        var placedEntities = _lookup.GetEntitiesInRange(Transform(ent).Coordinates,
            ent.Comp.WorkbenchRadius,
            LookupFlags.Uncontained);

        if (!CanCraftRecipe(recipe, placedEntities, args.User))
        {
            _popup.PopupEntity(Loc.GetString("cp14-workbench-cant-craft"), ent, args.User);
            return;
        }

        var resultEntities = new HashSet<EntityUid>();
        for (int i = 0; i < recipe.ResultCount; i++)
        {
            var resultEntity = Spawn(recipe.Result);
            resultEntities.Add(resultEntity);
        }

        foreach (var req in recipe.Requirements)
        {
            req.PostCraft(EntityManager, placedEntities, args.User);
        }

        //We teleport result to workbench AFTER craft.
        foreach (var resultEntity in resultEntities)
        {
            _transform.SetCoordinates(resultEntity, Transform(ent).Coordinates);
        }

        UpdateUIRecipes(ent, args.User);
        args.Handled = true;
    }

    private void StartCraft(Entity<CP14WorkbenchComponent> workbench,
        EntityUid user,
        CP14WorkbenchRecipePrototype recipe)
    {
        var craftDoAfter = new CP14CraftDoAfterEvent
        {
            Recipe = recipe.ID,
        };

        var doAfterArgs = new DoAfterArgs(EntityManager,
            user,
            recipe.CraftTime * workbench.Comp.CraftSpeed,
            craftDoAfter,
            workbench,
            workbench)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        _audio.PlayPvs(recipe.OverrideCraftSound ?? workbench.Comp.CraftSound, workbench);
    }

    private bool CanCraftRecipe(CP14WorkbenchRecipePrototype recipe, HashSet<EntityUid> entities, EntityUid user)
    {
        foreach (var req in recipe.Requirements)
        {
            if (!req.CheckRequirement(EntityManager, _proto, entities, user))
                return false;
        }

        return true;
    }
}
