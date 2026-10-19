using Godot;
using Pantheon.Game.Population;

namespace Pantheon.Client.Presentation.People;

/// <summary>
/// One person drawn on their own (ADR-049): the followed villager, a prophet or an elder. Its walk
/// keeps step with the ground it covers, so a person hurried by the simulation runs rather than
/// glides.
/// </summary>
internal sealed class PersonFigure
{
    private readonly MultiMesh _mesh;
    private readonly PersonLook _look;
    private readonly float _scale;
    private readonly float _stride;
    private float _strides;

    /// <summary>Creates the figure under a parent.</summary>
    /// <param name="parent">The node that carries it.</param>
    /// <param name="people">The person figures.</param>
    /// <param name="look">The person's look.</param>
    /// <param name="scale">The figure's size; the look's height is applied on top.</param>
    /// <param name="hideDistance">Beyond this distance from the camera the figure hides.</param>
    public PersonFigure(Node3D parent, PersonAssets people, PersonLook look, float scale, float hideDistance)
    {
        (_look, _scale) = (look, scale * look.Height);
        _stride = people.StrideLength * _scale;
        _mesh = people.NewMultiMesh();
        _mesh.InstanceCount = 1;
        _mesh.SetInstanceColor(0, PersonAssets.InstanceColor(look));
        parent.AddChild(new MultiMeshInstance3D { Name = "Person", Multimesh = _mesh, VisibilityRangeEnd = hideDistance });
    }

    /// <summary>The figure's height, in metres, without a hat.</summary>
    public float Height => Pantheon.Game.Presentation.PersonMesh.Height * _scale;

    /// <summary>Counts ground covered towards the walk's cycle.</summary>
    /// <param name="metres">Ground covered this frame.</param>
    public void Cover(float metres) => _strides += metres / (2f * _stride);

    /// <summary>Poses the figure.</summary>
    /// <param name="motion">What it does; walking uses the ground covered.</param>
    /// <param name="cycle">The cycle of any other motion.</param>
    /// <param name="lean">Forward lean in radians.</param>
    /// <param name="heading">Facing, in radians; 0 faces +Z.</param>
    public void Pose(PersonMotion motion, float cycle, float lean, float heading)
    {
        _mesh.SetInstanceTransform(0, new Transform3D(new Basis(Vector3.Up, heading).Scaled(Vector3.One * _scale), Vector3.Zero));
        _mesh.SetInstanceCustomData(0, PersonAssets.InstanceCustom(motion, motion == PersonMotion.Walking ? _strides : cycle, lean, _look));
    }
}
