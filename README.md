# Conclusions

Unity components have **major lifecycle events**: `FixedUpdate`, `Update`, and `LateUpdate`. These are never interleaved across different components. In other words, we call `FixedUpdate` for all components in the game, then we call `Update` for all components, then we call `LateUpdate` for all components.

`Awake` and `OnEnable` may be interleaved, but they are predictable: these are executed as soon as an object is instantiated.

`Start` is the most complicated event to understand. It's called at numerous points in the update cycle, based on the pseudocode described in [Game loop analysis](#game-loop-analysis). Between the major lifecycle phases of `FixedUpdate`, `Update`, and `LateUpdate`, there are implicit "startup" phases where any unstarted components receive `Start`. You might have unstarted components because they were instantiated _during_ the major lifecycle events themselves.

Let's get a bit recursive. What about components that were instantiated within the `Start` of another component?

One way to handle this would be to extend the implicit startup phase until there are no unstarted components left. This is **not** what Unity does.  Instead, one of two things happens.

First, the easy case: if your component doesn't define a callback for the upcoming lifecycle phase, then startup will be deferred to the next implicit startup phase. 

However, if it does define a callback for the upcoming lifecycle phase, then something complicated happens. Suppose your unstarted component defines `FixedUpdate`, and the next lifecycle phase is `FixedUpdate`. Then Unity will proceed with the `FixedUpdate` phase, but when it gets to your unstarted component, it will first call `Start`, and then call `FixedUpdate`. This means that your component's `Start` call is **interleaved** between calls of `FixedUpdate` to other methods.

I think it would've been better if Unity did not do this interleaving, and instead continued to loop over unstarted components and called `Start` on them until there were none left. (You may be worried about possible infinite loops, but those were already possible with `Awake` and `OnEnable`, so it's not a *new* problem.) There's a subtle problem with allowing the interleaving.

Let's suppose you have a `Creature` object, and it has two components, `CreatureBrain` and `CreatureMover`, that look like this:

```
public class CreatureBrain : MonoBehaviour {
  private BrainData _brainData;

  private void Start() {
    // initialize _brainData with something 
    _brainData = ...
  }
  
  public MovementData GetMovementStrategy() {
    // calculate some movement info based on current state
    return GetMovementDataFromBrainData(_brainData);
  }
}

public class CreatureMover : MonoBehaviour {
  private void FixedUpdate() {
    var strategy = GetComponent<CreatureBrain>().GetMovementStrategy();
    
    // do some movement stuff based on `strategy`
  }
```

In short: `CreatureMover` does stuff in `FixedUpdate`, but it needs to read information from `CreatureBrain` to do that.  Note how `CreatureBrain` *doesn't* implement a `FixedUpdate` callback--we'll get back to that fact in a second.

Now suppose that your creatures are instantiated in the `Start` callback of a spawner:

```
public class CreatureSpawner : MonoBehaviour {
  private void Start() {
    Instatiate(creaturePrefab);
  }
}
```

Suppose that `CreatureSpawner` is baked into the scene. Then the following sequence will occur when the scene begins:

- `CreatureSpawner` will instantiate a `Creature`, but since this is happening during `Start`, we will not call `Start` on the `Creature` itself--yet.
- The `FixedUpdate` lifecycle phase begins. `CreatureMover.FixedUpdate()` will get called. It will try to call `CreatureBrain.GetMovementStrategy()`, but that method depends on `_brainData`, which hasn't been initialized yet!
- `CreatureBrain.Start` will finally be called, and `CreatureMover._brain` will finally be initialized.

If we had added `CreatureBrain.FixedUpdate()`, even an empty one, then we couldn't get a null exception.

I believe that this is weird and surprising behavior. The fact that `CreatureBrain` does not define `FixedUpdate()` should not prevent its dependencies (like `CreatureMover`) from functioning properly.

It's worth addressing the obvious quibble to this example, which is: why not initialize in `Awake()` instead? I have two thoughts:

- Moving to `Awake` may fix this particular example, but there are more complicated scenarios where `Awake` isn't always an option. The fact that `Start` exists implies that there are things that can't always be done in `Awake`. Even more, having `Start` as a backup lets you be a little more relaxed with script execution order, which is a pain to maintain once the number of components is large.
- The fact that `Awake` may fix some or even most problematic cases does not make the behavior of `Start` any less confusing or weird. `Start` is strictly less useful, and strictly more dangerous, because of the way it functions currently.

# Game loop analysis

```
unityUpdate():
    // Phase 1: pre-FixedUpdate() Start() calls
    for c in allComponents:
      if c.Start is defined && !c.started:
        c.Start()
        
    allComponents += newlyInstantiatedComponents
      
    // Phase 2: FixedUpdate()
    for 0..numPhysicsFrames(Time.deltaTime):
      for c in allComponents:
        if c.FixedUpdate is not defined:
          continue
          
        // This only applies to objects added during
        // Phase 1. Objects added during this phase will
        // get a Start() call in Phase 3.
        if c.Start is defined && !c.started:
          c.Start()
        
        c.FixedUpdate()
      
      allComponents += newlyInstantiatedComponents
        
    // Phase 3: pre-Update() Start() calls
    for c in allComponents:
      if c.Start is defined && !c.started:
        c.Start() 
        
    allComponents += newlyInstantiatedComponents
        
    // Phase 4: Update()
    for c in allComponents:
      if c.Update is not defined:
        continue
        
        // This only applies to objects added during
        // Phase 3. Objects added during this phase will
        // get a Start() call in Phase 5.
        if c.Start is defined && !c.started:
          c.Start()
        
        c.Update()  
        
    allComponents += newlyInstantiatedComponents 
        
    // Phase 5: pre-LateUpdate() Start() calls
    for c in allComponents:
      if c.Start is defined && !c.started:
        c.Start()       
        
    allComponents += newlyInstantiatedComponents
        
    // Phase 6: LateUpdate()
    for c in allComponents:
      if c.LateUpdate is not defined:
        continue
        
        // This only applies to objects added during
        // Phase 3. Objects added during this phase will
        // get a Start() call in Phase 1 of next frame.
        if c.Start is defined && !c.started:
          c.Start()
        
        c.LateUpdate()   
        
    allComponents += newlyInstantiatedComponents         
```

# Setup

`SampleScene` contains a series of objects that write logs at different stages of the Unity lifecycle. Each object may have exactly one of the following components:

- `FrameStart` has the highest script execution order, and logs the `Awake`, `OnEnable`, `FixedUpdate`, `Update`, and `LateUpdate` events.
- Similarly, `FrameEnd` has the lowest script execution order, and logs the `Awake`, `OnEnable`, `FixedUpdate`, `Update`, and `LateUpdate` events.
- The following components have default script execution order, and log `Awake`, `OnEnable`, `Start`, and one of `FixedUpdate`, `Update`, or `LateUpdate` based on their name:
  - `LogFixedUpdate`
  - `LogUpdate`
  - `LogLateUpdate`
- `Spawner` spawns one instance each of `LogFixedUpdate`, `LogUpdate`, and `LogLateUpdate` during its `Awake`, `Start`, `FixedUpdate`, `Update`, and `LateUpdate`.

The scene is pre-populated with one object per component type, except for `FrameEnd`, which shares an object with `FrameStart`. The `Spawner` instance will spawn 15 additional objects before destroying itself.

# Results

```
Frame 0: FrameStart (-32226): FrameStart.Awake
Frame 0: Spawner (-23544): Spawning LogFixedUpdate-SpawnedDuringAwake
Frame 0: LogFixedUpdate-SpawnedDuringAwake(Clone) (-33562): Awake
Frame 0: LogFixedUpdate-SpawnedDuringAwake(Clone) (-33562): OnEnable
Frame 0: Spawner (-23544): Spawning LogUpdate-SpawnedDuringAwake
Frame 0: LogUpdate-SpawnedDuringAwake(Clone) (-33570): Awake
Frame 0: LogUpdate-SpawnedDuringAwake(Clone) (-33570): OnEnable
Frame 0: Spawner (-23544): Spawning LogLateUpdate-SpawnedDuringAwake
Frame 0: LogLateUpdate-SpawnedDuringAwake(Clone) (-33578): Awake
Frame 0: LogLateUpdate-SpawnedDuringAwake(Clone) (-33578): OnEnable
Frame 0: LogUpdate-InScene (-23534): Awake
Frame 0: LogUpdate-InScene (-23534): OnEnable
Frame 0: LogLateUpdate-InScene (-23524): Awake
Frame 0: LogLateUpdate-InScene (-23524): OnEnable
Frame 0: LogFixedUpdate-InScene (-23514): Awake
Frame 0: LogFixedUpdate-InScene (-23514): OnEnable
Frame 0: FrameEnd (-32216): FrameEnd.Awake

Frame 1: FrameStart (-32226): FrameStart.Start
Frame 1: Spawner (-23544): Spawning LogFixedUpdate-SpawnedDuringStart
Frame 1: LogFixedUpdate-SpawnedDuringStart(Clone) (-33600): Awake
Frame 1: LogFixedUpdate-SpawnedDuringStart(Clone) (-33600): OnEnable
Frame 1: Spawner (-23544): Spawning LogUpdate-SpawnedDuringStart
Frame 1: LogUpdate-SpawnedDuringStart(Clone) (-33608): Awake
Frame 1: LogUpdate-SpawnedDuringStart(Clone) (-33608): OnEnable
Frame 1: Spawner (-23544): Spawning LogLateUpdate-SpawnedDuringStart
Frame 1: LogLateUpdate-SpawnedDuringStart(Clone) (-33616): Awake
Frame 1: LogLateUpdate-SpawnedDuringStart(Clone) (-33616): OnEnable
Frame 1: LogFixedUpdate-SpawnedDuringAwake(Clone) (-33562): Start
Frame 1: LogFixedUpdate-SpawnedDuringAwake(Clone)-ClonedInStart(Clone) (-33624): Awake
Frame 1: LogFixedUpdate-SpawnedDuringAwake(Clone)-ClonedInStart(Clone) (-33624): OnEnable
Frame 1: LogUpdate-SpawnedDuringAwake(Clone) (-33570): Start
Frame 1: LogUpdate-SpawnedDuringAwake(Clone)-ClonedInStart(Clone) (-33632): Awake
Frame 1: LogUpdate-SpawnedDuringAwake(Clone)-ClonedInStart(Clone) (-33632): OnEnable
Frame 1: LogLateUpdate-SpawnedDuringAwake(Clone) (-33578): Start
Frame 1: LogLateUpdate-SpawnedDuringAwake(Clone)-ClonedInStart(Clone) (-33640): Awake
Frame 1: LogLateUpdate-SpawnedDuringAwake(Clone)-ClonedInStart(Clone) (-33640): OnEnable
Frame 1: LogUpdate-InScene (-23534): Start
Frame 1: LogUpdate-InScene-ClonedInStart(Clone) (-33648): Awake
Frame 1: LogUpdate-InScene-ClonedInStart(Clone) (-33648): OnEnable
Frame 1: LogLateUpdate-InScene (-23524): Start
Frame 1: LogLateUpdate-InScene-ClonedInStart(Clone) (-33656): Awake
Frame 1: LogLateUpdate-InScene-ClonedInStart(Clone) (-33656): OnEnable
Frame 1: LogFixedUpdate-InScene (-23514): Start
Frame 1: LogFixedUpdate-InScene-ClonedInStart(Clone) (-33664): Awake
Frame 1: LogFixedUpdate-InScene-ClonedInStart(Clone) (-33664): OnEnable
Frame 1: FrameEnd (-32216): FrameEnd.Start

Frame 1: FrameStart (-32226): FrameStart.FixedUpdate
Frame 1: Spawner (-23544): Spawning LogFixedUpdate-SpawnedDuringFixedUpdate
Frame 1: LogFixedUpdate-SpawnedDuringFixedUpdate(Clone) (-33672): Awake
Frame 1: LogFixedUpdate-SpawnedDuringFixedUpdate(Clone) (-33672): OnEnable
Frame 1: Spawner (-23544): Spawning LogUpdate-SpawnedDuringFixedUpdate
Frame 1: LogUpdate-SpawnedDuringFixedUpdate(Clone) (-33680): Awake
Frame 1: LogUpdate-SpawnedDuringFixedUpdate(Clone) (-33680): OnEnable
Frame 1: Spawner (-23544): Spawning LogLateUpdate-SpawnedDuringFixedUpdate
Frame 1: LogLateUpdate-SpawnedDuringFixedUpdate(Clone) (-33688): Awake
Frame 1: LogLateUpdate-SpawnedDuringFixedUpdate(Clone) (-33688): OnEnable
Frame 1: LogFixedUpdate-SpawnedDuringAwake(Clone) (-33562): FixedUpdate
Frame 1: LogFixedUpdate-InScene (-23514): FixedUpdate
Frame 1: LogFixedUpdate-SpawnedDuringStart(Clone) (-33600): Start
Frame 1: LogFixedUpdate-SpawnedDuringStart(Clone)-ClonedInStart(Clone) (-33696): Awake
Frame 1: LogFixedUpdate-SpawnedDuringStart(Clone)-ClonedInStart(Clone) (-33696): OnEnable
Frame 1: LogFixedUpdate-SpawnedDuringStart(Clone) (-33600): FixedUpdate
Frame 1: LogFixedUpdate-SpawnedDuringAwake(Clone)-ClonedInStart(Clone) (-33624): Start
Frame 1: LogFixedUpdate-SpawnedDuringAwake(Clone)-ClonedInStart(Clone) (-33624): FixedUpdate
Frame 1: LogFixedUpdate-InScene-ClonedInStart(Clone) (-33664): Start
Frame 1: LogFixedUpdate-InScene-ClonedInStart(Clone) (-33664): FixedUpdate
Frame 1: FrameEnd (-32216): FrameEnd.FixedUpdate

Frame 1: LogUpdate-SpawnedDuringStart(Clone) (-33608): Start
Frame 1: LogUpdate-SpawnedDuringStart(Clone)-ClonedInStart(Clone) (-33704): Awake
Frame 1: LogUpdate-SpawnedDuringStart(Clone)-ClonedInStart(Clone) (-33704): OnEnable
Frame 1: LogLateUpdate-SpawnedDuringStart(Clone) (-33616): Start
Frame 1: LogLateUpdate-SpawnedDuringStart(Clone)-ClonedInStart(Clone) (-33712): Awake
Frame 1: LogLateUpdate-SpawnedDuringStart(Clone)-ClonedInStart(Clone) (-33712): OnEnable
Frame 1: LogUpdate-SpawnedDuringAwake(Clone)-ClonedInStart(Clone) (-33632): Start
Frame 1: LogLateUpdate-SpawnedDuringAwake(Clone)-ClonedInStart(Clone) (-33640): Start
Frame 1: LogUpdate-InScene-ClonedInStart(Clone) (-33648): Start
Frame 1: LogLateUpdate-InScene-ClonedInStart(Clone) (-33656): Start
Frame 1: LogFixedUpdate-SpawnedDuringFixedUpdate(Clone) (-33672): Start
Frame 1: LogFixedUpdate-SpawnedDuringFixedUpdate(Clone)-ClonedInStart(Clone) (-33720): Awake
Frame 1: LogFixedUpdate-SpawnedDuringFixedUpdate(Clone)-ClonedInStart(Clone) (-33720): OnEnable
Frame 1: LogUpdate-SpawnedDuringFixedUpdate(Clone) (-33680): Start
Frame 1: LogUpdate-SpawnedDuringFixedUpdate(Clone)-ClonedInStart(Clone) (-33728): Awake
Frame 1: LogUpdate-SpawnedDuringFixedUpdate(Clone)-ClonedInStart(Clone) (-33728): OnEnable
Frame 1: LogLateUpdate-SpawnedDuringFixedUpdate(Clone) (-33688): Start
Frame 1: LogLateUpdate-SpawnedDuringFixedUpdate(Clone)-ClonedInStart(Clone) (-33736): Awake
Frame 1: LogLateUpdate-SpawnedDuringFixedUpdate(Clone)-ClonedInStart(Clone) (-33736): OnEnable
Frame 1: LogFixedUpdate-SpawnedDuringStart(Clone)-ClonedInStart(Clone) (-33696): Start

Frame 1: FrameStart (-32226): FrameStart.Update
Frame 1: Spawner (-23544): Spawning LogFixedUpdate-SpawnedDuringUpdate
Frame 1: LogFixedUpdate-SpawnedDuringUpdate(Clone) (-33744): Awake
Frame 1: LogFixedUpdate-SpawnedDuringUpdate(Clone) (-33744): OnEnable
Frame 1: Spawner (-23544): Spawning LogUpdate-SpawnedDuringUpdate
Frame 1: LogUpdate-SpawnedDuringUpdate(Clone) (-33752): Awake
Frame 1: LogUpdate-SpawnedDuringUpdate(Clone) (-33752): OnEnable
Frame 1: Spawner (-23544): Spawning LogLateUpdate-SpawnedDuringUpdate
Frame 1: LogLateUpdate-SpawnedDuringUpdate(Clone) (-33760): Awake
Frame 1: LogLateUpdate-SpawnedDuringUpdate(Clone) (-33760): OnEnable
Frame 1: LogUpdate-SpawnedDuringAwake(Clone) (-33570): Update
Frame 1: LogUpdate-InScene (-23534): Update
Frame 1: LogUpdate-SpawnedDuringStart(Clone) (-33608): Update
Frame 1: LogUpdate-SpawnedDuringAwake(Clone)-ClonedInStart(Clone) (-33632): Update
Frame 1: LogUpdate-InScene-ClonedInStart(Clone) (-33648): Update
Frame 1: LogUpdate-SpawnedDuringFixedUpdate(Clone) (-33680): Update
Frame 1: LogUpdate-SpawnedDuringStart(Clone)-ClonedInStart(Clone) (-33704): Start
Frame 1: LogUpdate-SpawnedDuringStart(Clone)-ClonedInStart(Clone) (-33704): Update
Frame 1: LogUpdate-SpawnedDuringFixedUpdate(Clone)-ClonedInStart(Clone) (-33728): Start
Frame 1: LogUpdate-SpawnedDuringFixedUpdate(Clone)-ClonedInStart(Clone) (-33728): Update
Frame 1: FrameEnd (-32216): FrameEnd.Update

Frame 1: LogLateUpdate-SpawnedDuringStart(Clone)-ClonedInStart(Clone) (-33712): Start
Frame 1: LogFixedUpdate-SpawnedDuringFixedUpdate(Clone)-ClonedInStart(Clone) (-33720): Start
Frame 1: LogLateUpdate-SpawnedDuringFixedUpdate(Clone)-ClonedInStart(Clone) (-33736): Start
Frame 1: LogFixedUpdate-SpawnedDuringUpdate(Clone) (-33744): Start
Frame 1: LogFixedUpdate-SpawnedDuringUpdate(Clone)-ClonedInStart(Clone) (-33768): Awake
Frame 1: LogFixedUpdate-SpawnedDuringUpdate(Clone)-ClonedInStart(Clone) (-33768): OnEnable
Frame 1: LogUpdate-SpawnedDuringUpdate(Clone) (-33752): Start
Frame 1: LogUpdate-SpawnedDuringUpdate(Clone)-ClonedInStart(Clone) (-33776): Awake
Frame 1: LogUpdate-SpawnedDuringUpdate(Clone)-ClonedInStart(Clone) (-33776): OnEnable
Frame 1: LogLateUpdate-SpawnedDuringUpdate(Clone) (-33760): Start
Frame 1: LogLateUpdate-SpawnedDuringUpdate(Clone)-ClonedInStart(Clone) (-33784): Awake
Frame 1: LogLateUpdate-SpawnedDuringUpdate(Clone)-ClonedInStart(Clone) (-33784): OnEnable

Frame 1: FrameStart (-32226): FrameStart.LateUpdate
Frame 1: Spawner (-23544): Spawning LogFixedUpdate-SpawnedDuringLateUpdate
Frame 1: LogFixedUpdate-SpawnedDuringLateUpdate(Clone) (-33792): Awake
Frame 1: LogFixedUpdate-SpawnedDuringLateUpdate(Clone) (-33792): OnEnable
Frame 1: Spawner (-23544): Spawning LogUpdate-SpawnedDuringLateUpdate
Frame 1: LogUpdate-SpawnedDuringLateUpdate(Clone) (-33800): Awake
Frame 1: LogUpdate-SpawnedDuringLateUpdate(Clone) (-33800): OnEnable
Frame 1: Spawner (-23544): Spawning LogLateUpdate-SpawnedDuringLateUpdate
Frame 1: LogLateUpdate-SpawnedDuringLateUpdate(Clone) (-33808): Awake
Frame 1: LogLateUpdate-SpawnedDuringLateUpdate(Clone) (-33808): OnEnable
Frame 1: LogLateUpdate-SpawnedDuringAwake(Clone) (-33578): LateUpdate
Frame 1: LogLateUpdate-InScene (-23524): LateUpdate
Frame 1: LogLateUpdate-SpawnedDuringStart(Clone) (-33616): LateUpdate
Frame 1: LogLateUpdate-SpawnedDuringAwake(Clone)-ClonedInStart(Clone) (-33640): LateUpdate
Frame 1: LogLateUpdate-InScene-ClonedInStart(Clone) (-33656): LateUpdate
Frame 1: LogLateUpdate-SpawnedDuringFixedUpdate(Clone) (-33688): LateUpdate
Frame 1: LogLateUpdate-SpawnedDuringStart(Clone)-ClonedInStart(Clone) (-33712): LateUpdate
Frame 1: LogLateUpdate-SpawnedDuringFixedUpdate(Clone)-ClonedInStart(Clone) (-33736): LateUpdate
Frame 1: LogLateUpdate-SpawnedDuringUpdate(Clone) (-33760): LateUpdate
Frame 1: LogLateUpdate-SpawnedDuringUpdate(Clone)-ClonedInStart(Clone) (-33784): Start
Frame 1: LogLateUpdate-SpawnedDuringUpdate(Clone)-ClonedInStart(Clone) (-33784): LateUpdate
Frame 1: FrameEnd (-32216): FrameEnd.LateUpdate

Frame 1: LogFixedUpdate-SpawnedDuringUpdate(Clone)-ClonedInStart(Clone) (-33768): Start
Frame 1: LogUpdate-SpawnedDuringUpdate(Clone)-ClonedInStart(Clone) (-33776): Start
Frame 1: LogFixedUpdate-SpawnedDuringLateUpdate(Clone) (-33792): Start
Frame 1: LogFixedUpdate-SpawnedDuringLateUpdate(Clone)-ClonedInStart(Clone) (-33816): Awake
Frame 1: LogFixedUpdate-SpawnedDuringLateUpdate(Clone)-ClonedInStart(Clone) (-33816): OnEnable
Frame 1: LogUpdate-SpawnedDuringLateUpdate(Clone) (-33800): Start
Frame 1: LogUpdate-SpawnedDuringLateUpdate(Clone)-ClonedInStart(Clone) (-33824): Awake
Frame 1: LogUpdate-SpawnedDuringLateUpdate(Clone)-ClonedInStart(Clone) (-33824): OnEnable
Frame 1: LogLateUpdate-SpawnedDuringLateUpdate(Clone) (-33808): Start
Frame 1: LogLateUpdate-SpawnedDuringLateUpdate(Clone)-ClonedInStart(Clone) (-33832): Awake
Frame 1: LogLateUpdate-SpawnedDuringLateUpdate(Clone)-ClonedInStart(Clone) (-33832): OnEnable
Frame 2: LogFixedUpdate-SpawnedDuringLateUpdate(Clone)-ClonedInStart(Clone) (-33816): Start
Frame 2: LogUpdate-SpawnedDuringLateUpdate(Clone)-ClonedInStart(Clone) (-33824): Start
Frame 2: LogLateUpdate-SpawnedDuringLateUpdate(Clone)-ClonedInStart(Clone) (-33832): Start

Frame 2: FrameStart (-32226): FrameStart.FixedUpdate
Frame 2: LogFixedUpdate-SpawnedDuringAwake(Clone) (-33562): FixedUpdate
Frame 2: LogFixedUpdate-InScene (-23514): FixedUpdate
Frame 2: LogFixedUpdate-SpawnedDuringStart(Clone) (-33600): FixedUpdate
Frame 2: LogFixedUpdate-SpawnedDuringAwake(Clone)-ClonedInStart(Clone) (-33624): FixedUpdate
Frame 2: LogFixedUpdate-InScene-ClonedInStart(Clone) (-33664): FixedUpdate
Frame 2: LogFixedUpdate-SpawnedDuringFixedUpdate(Clone) (-33672): FixedUpdate
Frame 2: LogFixedUpdate-SpawnedDuringStart(Clone)-ClonedInStart(Clone) (-33696): FixedUpdate
Frame 2: LogFixedUpdate-SpawnedDuringFixedUpdate(Clone)-ClonedInStart(Clone) (-33720): FixedUpdate
Frame 2: LogFixedUpdate-SpawnedDuringUpdate(Clone) (-33744): FixedUpdate
Frame 2: LogFixedUpdate-SpawnedDuringUpdate(Clone)-ClonedInStart(Clone) (-33768): FixedUpdate
Frame 2: LogFixedUpdate-SpawnedDuringLateUpdate(Clone) (-33792): FixedUpdate
Frame 2: LogFixedUpdate-SpawnedDuringLateUpdate(Clone)-ClonedInStart(Clone) (-33816): FixedUpdate
Frame 2: FrameEnd (-32216): FrameEnd.FixedUpdate

Frame 2: FrameStart (-32226): FrameStart.Update
Frame 2: LogUpdate-SpawnedDuringAwake(Clone) (-33570): Update
Frame 2: LogUpdate-InScene (-23534): Update
Frame 2: LogUpdate-SpawnedDuringStart(Clone) (-33608): Update
Frame 2: LogUpdate-SpawnedDuringAwake(Clone)-ClonedInStart(Clone) (-33632): Update
Frame 2: LogUpdate-InScene-ClonedInStart(Clone) (-33648): Update
Frame 2: LogUpdate-SpawnedDuringFixedUpdate(Clone) (-33680): Update
Frame 2: LogUpdate-SpawnedDuringStart(Clone)-ClonedInStart(Clone) (-33704): Update
Frame 2: LogUpdate-SpawnedDuringFixedUpdate(Clone)-ClonedInStart(Clone) (-33728): Update
Frame 2: LogUpdate-SpawnedDuringUpdate(Clone) (-33752): Update
Frame 2: LogUpdate-SpawnedDuringUpdate(Clone)-ClonedInStart(Clone) (-33776): Update
Frame 2: LogUpdate-SpawnedDuringLateUpdate(Clone) (-33800): Update
Frame 2: LogUpdate-SpawnedDuringLateUpdate(Clone)-ClonedInStart(Clone) (-33824): Update
Frame 2: FrameEnd (-32216): FrameEnd.Update

Frame 2: FrameStart (-32226): FrameStart.LateUpdate
Frame 2: LogLateUpdate-SpawnedDuringAwake(Clone) (-33578): LateUpdate
Frame 2: LogLateUpdate-InScene (-23524): LateUpdate
Frame 2: LogLateUpdate-SpawnedDuringStart(Clone) (-33616): LateUpdate
Frame 2: LogLateUpdate-SpawnedDuringAwake(Clone)-ClonedInStart(Clone) (-33640): LateUpdate
Frame 2: LogLateUpdate-InScene-ClonedInStart(Clone) (-33656): LateUpdate
Frame 2: LogLateUpdate-SpawnedDuringFixedUpdate(Clone) (-33688): LateUpdate
Frame 2: LogLateUpdate-SpawnedDuringStart(Clone)-ClonedInStart(Clone) (-33712): LateUpdate
Frame 2: LogLateUpdate-SpawnedDuringFixedUpdate(Clone)-ClonedInStart(Clone) (-33736): LateUpdate
Frame 2: LogLateUpdate-SpawnedDuringUpdate(Clone) (-33760): LateUpdate
Frame 2: LogLateUpdate-SpawnedDuringUpdate(Clone)-ClonedInStart(Clone) (-33784): LateUpdate
Frame 2: LogLateUpdate-SpawnedDuringLateUpdate(Clone) (-33808): LateUpdate
Frame 2: LogLateUpdate-SpawnedDuringLateUpdate(Clone)-ClonedInStart(Clone) (-33832): LateUpdate
Frame 2: FrameEnd (-32216): FrameEnd.LateUpdate

Frame 3: FrameStart (-32226): FrameStart.FixedUpdate
Frame 3: LogFixedUpdate-SpawnedDuringLateUpdate(Clone) (-33792): FixedUpdate
Frame 3: LogFixedUpdate-SpawnedDuringLateUpdate(Clone)-ClonedInStart(Clone) (-33816): FixedUpdate
Frame 3: FrameEnd (-32216): FrameEnd.FixedUpdate

Frame 3: FrameStart (-32226): FrameStart.Update
Frame 3: LogUpdate-SpawnedDuringLateUpdate(Clone) (-33800): Update
Frame 3: LogUpdate-SpawnedDuringLateUpdate(Clone)-ClonedInStart(Clone) (-33824): Update
Frame 3: FrameEnd (-32216): FrameEnd.Update

Frame 3: FrameStart (-32226): FrameStart.LateUpdate
Frame 3: LogLateUpdate-SpawnedDuringLateUpdate(Clone) (-33808): LateUpdate
Frame 3: LogLateUpdate-SpawnedDuringLateUpdate(Clone)-ClonedInStart(Clone) (-33832): LateUpdate
Frame 3: FrameEnd (-32216): FrameEnd.LateUpdate
```
