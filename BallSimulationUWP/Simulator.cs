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

            if (!(Math.Abs(length) > World.Epsilon))
            {
                return result;
            }

            result.X = input.X / length;
            result.Y = input.Y / length;

            return result;
        }

        public static float DotProduct(Vector2 a, Vector2 b)
        {
            return a.X * b.X + a.Y * b.Y;
        }
    }

    public class World
    {
        public static readonly float RealWorldGravity = 9.18f;

        public static float RealWorldScale = 10.0f;
        public static float DefaultGravity = RealWorldScale * RealWorldGravity;

        public static float Restitution = 0.85f;
        public static float Gravity = DefaultGravity;
        public static float Epsilon = 0.000009f;
        public static bool EnableCollisions = true;

        public int TickCounter { get; private set; }

        public const double DefaultWidth = 2048.0;
        public const double DefaultHeight = 2048.0;

        public IList<BallEntity> Entities = new List<BallEntity>();

        public readonly double WorldWidth;
        public readonly double WorldHeight;

        public World(double width = DefaultWidth, double height = DefaultHeight)
        {
            WorldWidth = width;
            WorldHeight = height;
        }

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

        private void CheckCollisions(float timeSimulationDivisor)
        {
            foreach (var entity in Entities)
            {
                entity.ApplyGravity(timeSimulationDivisor);
                entity.ApplyVelocity(timeSimulationDivisor);
            }

            for (var i = 0; i < Entities.Count; i++)
            {
                var b = Entities[i];

                for (var j = i + 1; j < Entities.Count; j++)
                {
                    if (!EnableCollisions) continue;

                    var bb = Entities[j];

                    if (b.IsColliding(bb))
                    {
                        b.Collide(bb);
                    }
                }
            }

            foreach (var entity in Entities)
            {
                entity.DetectWorldBoundCollision(this);
            }
        }

        public void AddEntity(BallEntity entity)
        {
            Entities.Add(entity);
        }

        public void RemoveEntity(BallEntity entity)
        {
            Entities.Remove(entity);
        }

        public void Tick(float timeSimulationDivisor)
        {
            TickCounter++;

            CheckCollisions(timeSimulationDivisor);
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
            Velocity = new Vector2();
        }

        public bool IsColliding(BallEntity entity)
        {
            var diffX = Position.X - entity.Position.X;
            var diffY = Position.Y - entity.Position.Y;
            var totalRadius = Radius + entity.Radius;
            var radiusSquared = totalRadius * totalRadius;
            var distanceSquared = diffX * diffX + diffY * diffY;

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

            if (VectorUtilities.DotProduct(delta, delta) > totalRadius * totalRadius)
            {
                return;
            }

            Vector2 minimumTranslationDistance;
            var isOnTopOfEachOther = Math.Abs(distance) <= World.Epsilon;

            if (!isOnTopOfEachOther)
            {
                minimumTranslationDistance = delta * ((Radius + entity.Radius - distance) / distance);
            }
            else
            {
                distance = entity.Radius + Radius - 1.0f;
                delta = new Vector2(entity.Radius + Radius, 0.0f);
                minimumTranslationDistance = delta * ((Radius + entity.Radius - distance) / distance);
            }

            var inverseMassA = 1 / Mass;
            var inverseMassB = 1 / entity.Mass;
            var inverseMassTotal = inverseMassA + inverseMassB;

            var targetPositionA = Position + minimumTranslationDistance * (inverseMassA / inverseMassTotal);
            var targetPositionB = entity.Position - minimumTranslationDistance * (inverseMassB / inverseMassTotal);

            var impactSpeed = Velocity - entity.Velocity;
            var velocityNumber = VectorUtilities.DotProduct(impactSpeed,
                VectorUtilities.Normalize(minimumTranslationDistance));

            Position = targetPositionA;
            entity.Position = targetPositionB;

            if (velocityNumber > World.Epsilon)
            {
                Updated = entity.Updated = true;
                return;
            }

            var impulseFactor = -(1.0f * World.Restitution) * velocityNumber / inverseMassTotal;
            var impulse = Vector2.Normalize(minimumTranslationDistance) * impulseFactor;

            if (float.IsNaN(impulse.Length()))
            {
                impulse = new Vector2(0.0f, 0.0f);
            }

            var deltaVelocityA = impulse * inverseMassA;
            var deltaVelocityB = -(impulse * inverseMassB);
            var targetVelocityA = Velocity + deltaVelocityA;
            var targetVelocityB = entity.Velocity + deltaVelocityB;

            Velocity = targetVelocityA;
            entity.Velocity = targetVelocityB;

            Updated = entity.Updated = true;
        }

        public void ApplyVelocity(float timeSimulationDivisor)
        {
            if (Math.Abs(Velocity.X) < World.Epsilon)
            {
                Velocity.X = 0.0f;
                Updated = true;
            }
            else
            {
                var delta = Velocity.X / timeSimulationDivisor;
                Position.X += delta;
                Updated = true;
            }

            if (Math.Abs(Velocity.Y) < World.Epsilon)
            {
                Velocity.Y = 0.0f;
                Updated = true;
            }
            else
            {
                var delta = Velocity.Y / timeSimulationDivisor;
                Position.Y += delta;
                Updated = true;
            }
        }

        public void ApplyGravity(float timeSimulationDivisor)
        {
            if (Math.Abs(World.Gravity) > World.Epsilon)
            {
                Velocity.Y = Velocity.Y + World.Gravity / timeSimulationDivisor;
            }
        }

        public void DetectWorldBoundCollision(World world)
        {
            var r2 = Radius * 2;
            if (Position.X - r2 < World.Epsilon)
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

            if (Position.Y - r2 < World.Epsilon)
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
        }
    }

    public class Simulator
    {
        public static int TickRate = 200;

        public readonly World World;
        public Action OnTickCallback;
        private readonly DispatcherTimer _timer;

        public IEnumerable<BallEntity> Entities => World.Entities;

        public Simulator(World world)
        {
            World = world;
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000.0 / TickRate)
            };
            _timer.Tick += OnTick;
            _timer.Start();
        }

        private void OnTick(object state, object e)
        {
            World.Tick(TickRate / World.RealWorldScale);

            OnTickCallback?.Invoke();
        }

        public void AddBall(BallEntity entity)
        {
            World.AddEntity(entity);
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
            foreach (var entity in Entities)
            {
                entity.Velocity = new Vector2();
                entity.Updated = true;
            }
        }

        public void RemoveBall(BallEntity entity)
        {
            World.RemoveEntity(entity);
        }
    }
}