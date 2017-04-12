using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Windows.UI.Xaml;

namespace BallSimulationUWP
{
    public class VectorUtilities
    {
        public static Vector2 Normalize(Vector2 input)
        {
            var result = new Vector2();
            var length = input.Length();

            if (Math.Abs(length) > World.Epsilon)
            {
                result.X = input.X / length;
                result.Y = input.Y / length;
            }

            return result;
        }

        public static float DotProduct(Vector2 a, Vector2 b)
        {
            return a.X * b.X + a.Y * b.Y;
        }
    }

    public class World
    {
        public static float Restitution = 0.85f;
        public static float Gravity = 3.33333f;
        public static float Epsilon = 0.000009f;
        public static float TerminalVelocity = 50.0f;
        public static bool EnableCollisions = true;

        public int TickCounter { get; private set; }

        public IList<BallEntity> Entities = new List<BallEntity>();

        public double WorldWidth = 1024;
        public double WorldHeight = 1024;

        public void Scatter()
        {
            var random = new Random();

            foreach (var entity in Entities)
            {
                var x = random.NextDouble() * WorldWidth;
                var y = random.NextDouble() * WorldHeight;
                entity.Position = new Vector2((float) x, (float) y);
                entity.Updated = true;
            }
        }

        private void CheckCollisions()
        {
            for (var i = 0; i < Entities.Count; i++)
            {
                var b = Entities[i];

                b.Tick(this);

                for (var j = i + 1; j < Entities.Count; j++)
                {
                    if (!World.EnableCollisions) continue;

                    var bb = Entities[j];

                    if (b.IsColliding(bb))
                    {
                        b.Collide(bb);
                    }
                }
            }
        }

        public void AddEntity(BallEntity entity)
        {
            Entities.Add(entity);
        }

        public void Tick()
        {
            TickCounter++;

            CheckCollisions();
        }
    }

    public class BallEntity
    {
        public readonly float Mass;

        public Vector2 Position;
        public Vector2 Velocity;
        public int RemoteId;
        public bool Updated;

        public float Radius;

        public BallEntity(float mass, float radius, Vector2 position)
        {
            Position = position;
            Radius = radius;
            Mass = mass;
            Velocity = new Vector2(0.0f, 0.0f);
        }

        public bool IsColliding(BallEntity entity)
        {
            var diffX = Position.X - entity.Position.X;
            var diffY = Position.Y - entity.Position.Y;
            var totalRadius = Radius + entity.Radius;
            var radiusSquared = totalRadius * totalRadius;
            var distanceSquared = (diffX * diffX) + (diffY * diffY);

            return distanceSquared <= radiusSquared;
        }

        public bool GetUpdateFlag()
        {
            if (!Updated) return false;
            Updated = false;
            return true;
        }

        public void Collide(BallEntity entity)
        {
            var totalRadius = Radius + entity.Radius;
            var delta = Position - entity.Position;
            var distance = delta.Length();

            if (VectorUtilities.DotProduct(delta, delta) > (totalRadius * totalRadius))
            {
                return;
            }

            Vector2 minimumTranslationDistance;

            if (Math.Abs(distance) > World.Epsilon)
            {
                minimumTranslationDistance = delta * ((Radius + entity.Radius) - distance) / distance;
            }
            else
            {
                distance = entity.Radius + Radius - 1.0f;
                delta = new Vector2(entity.Radius + Radius, 0.0f);
                minimumTranslationDistance = delta * (((Radius + entity.Radius) - distance) / distance);
            }

            var inverseMassA = 1 / Mass;
            var inverseMassB = 1 / entity.Mass;
            var inverseMassTotal = inverseMassA + inverseMassB;

            var targetPositionA = Position + (minimumTranslationDistance * (inverseMassA / inverseMassTotal));
            var targetPositionB = entity.Position + (minimumTranslationDistance * (inverseMassB / inverseMassTotal));

            var impactSpeed = Velocity - entity.Velocity;
            var velocityNumber = VectorUtilities.DotProduct(impactSpeed,
                VectorUtilities.Normalize(minimumTranslationDistance));

            if (velocityNumber > 0.0f)
            {
                return;
            }

            var impulse = minimumTranslationDistance *
                          ((-(1.0f + World.Restitution) * velocityNumber) / inverseMassTotal);

            var targetVelocityA = Velocity + (impulse * inverseMassA);
            var targetVelocityB = entity.Velocity - (impulse * inverseMassB);

            Position = targetPositionA;
            entity.Position = targetPositionB;

            Velocity = targetVelocityA;
            entity.Velocity = targetVelocityB;

            Updated = entity.Updated = true;
        }

        public void Tick(World world)
        {
            var r2 = Radius * 2;
            if (Position.X - r2 < 0)
            {
                Position.X = r2;
                Velocity.X = -(Velocity.X * World.Restitution);
                Velocity.Y = Velocity.Y * World.Restitution;
                Updated = true;
            }
            else if (Position.X + r2 > world.WorldWidth)
            {
                Position.X = (float) world.WorldWidth - r2;
                Velocity.X = -(Velocity.X * World.Restitution);
                Velocity.Y = Velocity.Y * World.Restitution;
                Updated = true;
            }

            if (Position.Y - r2 < 0)
            {
                Position.Y = r2;
                Velocity.Y = -(Velocity.Y * World.Restitution);
                Velocity.X = Velocity.X * World.Restitution;
                Updated = true;
            }
            else if (Position.Y + r2 > world.WorldHeight)
            {
                Position.Y = (float) world.WorldHeight - r2;
                Velocity.Y = -(Velocity.Y * World.Restitution);
                Velocity.X = Velocity.X * World.Restitution;
                Updated = true;
            }

            if (Math.Abs(Velocity.X) < World.Epsilon)
            {
                Velocity.X = 0;
                Updated = true;
            }

            if (Math.Abs(Velocity.Y) < World.Epsilon)
            {
                Velocity.Y = 0;
                Updated = true;
            }

            if (Math.Abs(Velocity.X) > World.TerminalVelocity)
            {
                Velocity.X = Velocity.X < 0 ? -World.TerminalVelocity : World.TerminalVelocity;
                Updated = true;
            }

            if (Math.Abs(Velocity.Y) > World.TerminalVelocity)
            {
                Velocity.Y = Velocity.Y < 0 ? -World.TerminalVelocity : World.TerminalVelocity;
                Updated = true;
            }

            Velocity.Y = Velocity.Y + World.Gravity;

            if (Math.Abs(Velocity.X) > World.Epsilon)
            {
                Position.X = Position.X + (Velocity.X / 4);
                Updated = true;
            }

            if (Math.Abs(Velocity.Y) > World.Epsilon)
            {
                Position.Y = Position.Y + (Velocity.Y / 4);
                Updated = true;
            }
        }
    }

    public class Simulator
    {
        public readonly World World;
        public Action OnTickCallback;
        private readonly DispatcherTimer _timer;

        public Simulator(World world)
        {
            World = world;
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16.0)
            };
            _timer.Tick += OnTick;
            _timer.Start();
        }

        private void OnTick(object state, object e)
        {
            World.Tick();

            OnTickCallback?.Invoke();
        }

        public void AddBall(BallEntity entity)
        {
            World.AddEntity(entity);
        }

        public IEnumerable<BallEntity> Entities()
        {
            return World.Entities;
        }

        public void Toggle()
        {
            if (_timer.IsEnabled)
            {
                _timer.Stop();
            }
            else
            {
                _timer.Start();
            }
        }

        public void ZeroVelocity()
        {
            foreach (var entity in Entities())
            {
                entity.Velocity = new Vector2(0.0f);
                entity.Updated = true;
            }
        }
    }
}