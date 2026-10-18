using Godot;

namespace Pantheon.Client.Presentation.Divine;

/// <summary>
/// The god's hand (spec §55), for now a visual only (ADR-010): a glowing, translucent hand that
/// hovers over the cursor, presses down when a miracle is cast, pats the Creature to reward it
/// and swats it to punish it. Its size follows the camera distance so it reads at every zoom.
/// </summary>
internal sealed partial class DivineHand : Node3D
{
    private const float HoverHeight = 0.08f;
    private const float SizePerMetreOfDistance = 0.022f;
    private const float GestureSeconds = 1.1f;

    private Gesture _gesture;
    private float _age;
    private Vector3 _gestureTarget;
    private float _gestureLift;
    private Vector3 _cursor;
    private bool _hasCursor;
    private float _cameraDistance = 300f;
    private float _gestureSize;

    private enum Gesture
    {
        None,
        Press,
        Pat,
        Swat,
    }

    /// <summary>Creates the hand.</summary>
    /// <returns>The hand node, ready to be added to the tree.</returns>
    public static DivineHand Create()
    {
        var hand = new DivineHand { Name = "DivineHand", Visible = false };
        var skin = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.92f, 0.72f, 0.55f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = new Color(1f, 0.82f, 0.45f),
            EmissionEnergyMultiplier = 0.6f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        var shape = new Node3D { Name = "Shape", Rotation = new Vector3(-0.5f, 0f, 0f) };
        hand.AddChild(shape);
        shape.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1f, 0.32f, 1.1f), Material = skin }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        for (var i = 0; i < 4; i++)
        {
            var finger = new MeshInstance3D
            {
                Mesh = new CapsuleMesh { Radius = 0.11f, Height = 0.75f - (i == 3 ? 0.15f : 0f), Material = skin },
                Position = new Vector3(-0.36f + (i * 0.24f), 0f, 0.85f),
                Rotation = new Vector3(Mathf.Pi / 2f, 0f, 0f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            shape.AddChild(finger);
        }

        shape.AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.12f, Height = 0.6f, Material = skin },
            Position = new Vector3(-0.62f, 0f, 0.15f),
            Rotation = new Vector3(Mathf.Pi / 2f, 0f, 0.7f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        return hand;
    }

    /// <summary>Puts the hand over the cursor point, or hides it when there is none.</summary>
    /// <param name="cursor">Ground point under the cursor.</param>
    /// <param name="cameraDistance">Camera distance in metres, for the hand's size.</param>
    public void Follow(Vector3? cursor, float cameraDistance)
    {
        _cameraDistance = cameraDistance;
        _hasCursor = cursor is not null;
        if (cursor is { } point)
        {
            _cursor = point;
        }
    }

    /// <summary>Presses down at a point, as when casting a miracle.</summary>
    /// <param name="point">Ground point.</param>
    public void Press(Vector3 point) => Start(Gesture.Press, point, 0f);

    /// <summary>Pats a Creature on the head; the hand grows to a third of the Creature's height.</summary>
    /// <param name="head">Creature's position.</param>
    /// <param name="headHeight">Height of its head.</param>
    public void Pat(Vector3 head, float headHeight) => Start(Gesture.Pat, head, headHeight, headHeight / 3f);

    /// <summary>Swats a Creature; the hand grows to a third of the Creature's height.</summary>
    /// <param name="head">Creature's position.</param>
    /// <param name="headHeight">Height of its head.</param>
    public void Swat(Vector3 head, float headHeight) => Start(Gesture.Swat, head, headHeight, headHeight / 3f);

    /// <summary>Animates the gesture, or hovers over the cursor.</summary>
    /// <param name="delta">Frame time in seconds.</param>
    public override void _Process(double delta)
    {
        var size = Mathf.Max(Mathf.Clamp(_cameraDistance * SizePerMetreOfDistance, 1.5f, 40f), _gesture == Gesture.None ? 0f : _gestureSize);
        Scale = new Vector3(size, size, size);
        if (_gesture != Gesture.None)
        {
            _age += (float)delta;
            var t = Mathf.Clamp(_age / GestureSeconds, 0f, 1f);
            Visible = true;
            Animate(t, size);
            if (t >= 1f)
            {
                _gesture = Gesture.None;
            }

            return;
        }

        Visible = _hasCursor;
        Position = _cursor + new Vector3(0f, (HoverHeight * _cameraDistance) + size, 0f);
        Rotation = Vector3.Zero;
    }

    /// <summary>Starts a gesture.</summary>
    private void Start(Gesture gesture, Vector3 target, float lift, float size = 0f)
    {
        _gestureSize = size;
        _gesture = gesture;
        _age = 0f;
        _gestureTarget = target;
        _gestureLift = lift;
    }

    /// <summary>Places the hand along its gesture at progress <paramref name="t"/>.</summary>
    private void Animate(float t, float size)
    {
        var top = _gestureTarget + new Vector3(0f, _gestureLift + (size * 2.5f), 0f);
        var contact = _gestureTarget + new Vector3(0f, _gestureLift + (size * 0.4f), 0f);
        switch (_gesture)
        {
            case Gesture.Press:
                Position = top.Lerp(contact, Mathf.Sin(t * Mathf.Pi));
                Rotation = Vector3.Zero;
                break;
            case Gesture.Pat:
                Position = top.Lerp(contact, Mathf.Abs(Mathf.Sin(t * Mathf.Pi * 3f)));
                Rotation = Vector3.Zero;
                break;
            case Gesture.Swat:
                var sweep = Mathf.Sin(t * Mathf.Pi);
                Position = contact + new Vector3(size * 3f * (1f - (2f * t)), size * (1f - sweep), 0f);
                Rotation = new Vector3(0f, 0f, Mathf.Pi / 2f * sweep);
                break;
            case Gesture.None:
                break;
        }
    }
}
