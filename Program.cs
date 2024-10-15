using System;
using System.Collections.Generic;
using System.Linq;
using StereoKit;

// Initialize StereoKit
SKSettings settings = new SKSettings
{
    appName = "flashcards",
    assetsFolder = "Assets",
};
if (!SK.Initialize(settings))
    return;

// Setup physics
BepuUtilities.Memory.BufferPool pool = new();
BepuPhysics.Simulation sim = BepuPhysics.Simulation.Create(pool, new NarrowPhaseCallbacks(), new PoseIntegratorCallbacks(new Vec3(0, -10, 0)), new BepuPhysics.SolveDescription(8, 1));
double simTime = 0;
float simStep = 1.0f / 90.0f;
BepuUtilities.ThreadDispatcher dispatcher = new BepuUtilities.ThreadDispatcher(Environment.ProcessorCount);


// Create assets used by the app
Pose cubePose = new Pose(0, 0, -0.5f);
Model cube = Model.FromMesh(
    Mesh.GenerateRoundedCube(Vec3.One * 0.1f, 0.02f),
    Material.UI);

Matrix floorTransform = Matrix.TS(0, -1.5f, 0, new Vec3(30, 0.1f, 30));
Material floorMaterial = new Material("floor.hlsl");
floorMaterial.Transparency = Transparency.Blend;
sim.Statics.Add(new BepuPhysics.StaticDescription(floorTransform.Pose.position, Quat.Identity, sim.Shapes.Add(new BepuPhysics.Collidables.Box(30, 0.1f, 30))));

Vehicle vehicle = new Vehicle();


WoodBlock test = new WoodBlock(vehicle, new Vec3(0.1f, 0.0f, 0.0f));
// Core application loop
SK.Run(() =>
{
    if (Device.DisplayBlend == DisplayBlend.Opaque)
        Mesh.Cube.Draw(floorMaterial, floorTransform);

    while (simTime < Time.Total)
    {
        sim.Timestep(simStep, dispatcher);
        simTime += simStep;
    }

    vehicle.draw();
});

class Vehicle
{
    public Dictionary<Vec3, Block> blockMap;
    public Pose pose = new Pose(0, 0, 0);

    public Vehicle()
    {
        this.blockMap = new Dictionary<Vec3, Block>();
        new CoreBlock(this, new Vec3(0, 0, 0));
    }
    public void draw()
    {
        List<Block> blocks = this.blockMap.Values.ToList();
        Hierarchy.Push(this.pose.ToMatrix());
        foreach (Block block in blocks)
        {
            Pose pose = block.grabbingLogic();
            block.draw(pose);
        }
        Hierarchy.Pop();
    }
}

class Block
{
    protected Vehicle parent;
    protected Vec3 position;
    protected Material material;
    protected Tex texture;
    protected Model model;

    public Block(Vehicle parent, Vec3 position)
    {
        this.material = Material.PBR.Copy();
        this.parent = parent;
        this.position = position;
        this.setPosition(position);
    }

    public void setPositionWhateverOffset(Vec3 position)
    {
        this.parent.blockMap.Remove(this.position);
        Random rnd = new Random();
        while (this.parent.blockMap.ContainsKey(position))
        {
            float val = rnd.NextSingle();
            if (val <= 0.16)
            {
                position.y += 0.1f;
            }
            else if (val <= 0.32)
            {
                position.y -= 0.1f;
            }
            else if (val <= 0.48)
            {
                position.x += 0.1f;
            }
            else if (val <= 0.64)
            {
                position.x -= 0.1f;
            }
            else if (val <= 0.80)
            {
                position.z += 0.1f;
            }
            else
            {
                position.z -= 0.1f;
            }
        }
        this.setPosition(position);
    }

    public void setPosition(Vec3 position)
    {
        this.parent.blockMap.Remove(this.position);
        this.position = position;
        this.parent.blockMap.Add(position, this);
    }

    private bool grabbedLast = false;
    private Pose lastPose = new Pose(new Vec3(0, 0, 0));

    public virtual Pose grabbingLogic()
    {
        Pose cubePose = new Pose(this.position);
        bool grabbed = UI.Handle(string.Format("{0:N3}", this.position), ref cubePose, this.model.Bounds);
        if (!grabbed && this.grabbedLast)
        {
            cubePose.position.x = (float)Math.Round(this.lastPose.position.x, 1);
            cubePose.position.y = (float)Math.Round(this.lastPose.position.y, 1);
            cubePose.position.z = (float)Math.Round(this.lastPose.position.z, 1);
            Console.WriteLine("{0:N3}", this.lastPose.position);
            this.setPositionWhateverOffset(cubePose.position);
        }
        lastPose = cubePose;
        this.grabbedLast = grabbed;
        return cubePose;
    }

    public void draw(Pose pose)
    {
        this.model.Draw(pose.ToMatrix());
    }
}

class CoreBlock : Block
{
    public CoreBlock(Vehicle parent, Vec3 position) : base(parent, position)
    {
        this.texture = Tex.FromFile("Galvanized Metal/Metal_Galvanized_DIFF.jpg");
        this.material[MatParamName.DiffuseTex] = this.texture;
        this.model = Model.FromMesh(Mesh.GenerateRoundedCube(Vec3.One * 0.1f, 0.01f), this.material);
    }
    public override Pose grabbingLogic()
    {
        return new Pose(this.position);
    }
}

class WoodBlock : Block
{
    public WoodBlock(Vehicle parent, Vec3 position) : base(parent, position)
    {
        this.texture = Tex.FromFile("Wood_Grain/Wood_Grain_DIFF.png");
        this.material[MatParamName.DiffuseTex] = this.texture;
        this.model = Model.FromMesh(Mesh.GenerateRoundedCube(Vec3.One * 0.1f, 0.01f), this.material);
    }
}
