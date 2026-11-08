using System;
using System.Collections.Generic;
using System.Linq;
using WhereAmI.Input;

namespace WhereAmI.Game;

/// <summary>Enemies, their attacks, the player's eggs and everything that hurts.</summary>
public sealed partial class GameWorld
{
    public List<Projectile> Projectiles { get; } = new();

    /// <summary>Area an owl's gust currently covers, for drawing; empty when no owl is flapping.</summary>
    public List<(Box Area, int Direction)> Gusts { get; } = new();

    // ───────────────────────────── Eggs ─────────────────────────────

    private void UpdateEggs(double dt, IInput input)
    {
        var p = Player;
        p.ThrowCooldown = Math.Max(0, p.ThrowCooldown - dt);
        p.ThrowTime = Math.Max(0, p.ThrowTime - dt);

        if (p.Eggs < Tuning.MaxEggs)
        {
            p.EggRecharge += dt;
            if (p.EggRecharge >= Tuning.EggRecharge)
            {
                p.Eggs++;
                p.EggRecharge = 0;
            }
        }

        if (!input.ConsumePressed(GameAction.Throw) || p.Eggs == 0 || p.ThrowCooldown > 0)
            return;

        var direction = p.FacingLeft ? -1 : 1;
        var b = p.Bounds;
        Projectiles.Add(new Projectile
        {
            Kind = ProjectileKind.Egg,
            X = b.CenterX + direction * 18 - Tuning.EggWidth / 2,
            Y = b.Top + 10,
            Width = Tuning.EggWidth,
            Height = Tuning.EggHeight,
            VelocityX = direction * Tuning.EggSpeedX + p.VelocityX * 0.3,
            VelocityY = -Tuning.EggSpeedY,
        });

        p.Eggs--;
        p.ThrowCooldown = Tuning.EggCooldown;
        p.ThrowTime = 0.3;
        _events.Add(GameEvent.EggThrown);
    }

    private void UpdateProjectiles(double dt)
    {
        foreach (var shot in Projectiles)
        {
            shot.Age += dt;
            if (shot.Kind == ProjectileKind.Egg)
                UpdateEgg(shot, dt);
            else
                UpdateEmber(shot, dt);

            if (shot.Y > Level.Height || shot.X < -100 || shot.X > Level.Width + 100)
                shot.Gone = true;
        }

        Projectiles.RemoveAll(s => s.Gone);
    }

    private void UpdateEgg(Projectile egg, double dt)
    {
        if (egg.Age > Tuning.EggLifetime)
        {
            BreakEgg(egg);
            return;
        }

        egg.VelocityY += Tuning.EggGravity * dt;

        // Sideways: hitting a wall breaks the egg.
        egg.X += egg.VelocityX * dt;
        if (Solids().Any(egg.Bounds.Overlaps))
        {
            BreakEgg(egg);
            return;
        }

        // Up and down: it bounces once on the ground, and breaks on anything above it.
        egg.Y += egg.VelocityY * dt;
        foreach (var solid in Solids())
        {
            if (!egg.Bounds.Overlaps(solid))
                continue;

            if (egg.VelocityY < 0)
            {
                // Eggs knock coins out of boxes too.
                var box = RewardBoxes.FirstOrDefault(r => r.IsRevealed && r.Bounds == solid);
                if (box is not null)
                    ShakeBox(box);
                BreakEgg(egg);
                return;
            }

            if (egg.Bounces > 0)
            {
                BreakEgg(egg);
                return;
            }

            egg.Y = solid.Top - egg.Height;
            egg.VelocityY *= -0.45;
            egg.VelocityX *= 0.75;
            egg.Bounces++;
            break;
        }

        foreach (var enemy in Enemies)
        {
            if (!enemy.Alive || !enemy.IsTangible || !egg.Bounds.Overlaps(enemy.Bounds))
                continue;

            if (!enemy.DeflectsEggs)
                DefeatEnemy(enemy, GameEvent.EnemyHitByEgg);
            BreakEgg(egg);
            return;
        }
    }

    private void BreakEgg(Projectile egg)
    {
        egg.Gone = true;
        var b = egg.Bounds;
        for (var i = 0; i < 3; i++)
        {
            Particles.Add(new Particle
            {
                Kind = ParticleKind.Shell,
                Variant = i,
                X = b.CenterX,
                Y = b.CenterY,
                VelocityX = (i - 1) * 110 + (_random.NextDouble() - 0.5) * 60,
                VelocityY = -220 - _random.NextDouble() * 140,
                Gravity = 1400,
                Size = 14,
                Life = 0.7,
                Spin = (_random.NextDouble() - 0.5) * 900,
            });
        }

        _events.Add(GameEvent.EggBroke);
    }

    /// <summary>A box was hit (by the player's head or an egg): out comes a coin, if any are left.</summary>
    private void ShakeBox(RewardBox box)
    {
        var b = box.Bounds;
        if (box.CoinsLeft > 0)
        {
            box.CoinsLeft--;
            Points++;
            CoinsCollected++;
            Effects.Add(new Effect(EffectKind.PoppedCoin, b.CenterX - Tuning.CoinSize / 2, b.Top - Tuning.CoinSize, 0.4));
            SpawnSparks(b.CenterX, b.Top - 8, 8);
            _events.Add(GameEvent.CoinCollected);
        }
        else
        {
            _events.Add(GameEvent.EmptyBoxHit);
        }
    }

    private void UpdateEmber(Projectile ember, double dt)
    {
        ember.X += ember.VelocityX * dt;
        ember.Y += ember.VelocityY * dt;

        if (ember.Age > 5 || Solids().Any(ember.Bounds.Overlaps))
        {
            ember.Gone = true;
            SpawnSparks(ember.Bounds.CenterX, ember.Bounds.CenterY, 6);
            return;
        }

        var p = Player;
        if (!p.IsDead && !IsCompleted && p.Invulnerable <= 0 && ember.Bounds.Overlaps(p.Bounds))
        {
            ember.Gone = true;
            Kill();
        }
    }

    // ───────────────────────────── Enemies ─────────────────────────────

    /// <param name="aggressive">False behind the menus: enemies move but do not attack.</param>
    private void UpdateEnemies(double dt, bool aggressive)
    {
        Gusts.Clear();
        foreach (var enemy in Enemies)
        {
            if (!enemy.Alive)
                continue;

            enemy.AnimationTime += dt;
            enemy.Timer += dt;

            switch (enemy.Kind)
            {
                case EnemyKind.Ghost: UpdateGhost(enemy, dt); break;
                case EnemyKind.Scribble: UpdateScribble(enemy, dt, aggressive); break;
                case EnemyKind.Moth: UpdateMoth(enemy, aggressive); break;
                case EnemyKind.Owl: UpdateOwl(enemy, aggressive); break;
                case EnemyKind.Burr: UpdateBurr(enemy, dt); break;
                case EnemyKind.Toad: UpdateToad(enemy, dt, aggressive); break;
            }
        }
    }

    /// <summary>Walks between MinX and MaxX, turning around at both ends.</summary>
    private static void Patrol(Enemy enemy, double dt, double speed)
    {
        enemy.X += Math.Sign(enemy.VelocityX == 0 ? 1 : enemy.VelocityX) * speed * dt;
        if (enemy.X <= enemy.MinX)
        {
            enemy.X = enemy.MinX;
            enemy.VelocityX = speed;
        }
        else if (enemy.X >= enemy.MaxX)
        {
            enemy.X = enemy.MaxX;
            enemy.VelocityX = -speed;
        }
        else
        {
            enemy.VelocityX = Math.Sign(enemy.VelocityX == 0 ? 1 : enemy.VelocityX) * speed;
        }

        enemy.FacingLeft = enemy.VelocityX < 0;
    }

    private static void UpdateGhost(Enemy ghost, double dt)
    {
        Patrol(ghost, dt, Tuning.EnemySpeed);
        ghost.Y = ghost.HomeY + Math.Sin(ghost.AnimationTime * 2.2) * 10;

        // Every six seconds it fades out for a moment.
        ghost.Special = ghost.Timer % 6 > 4.6;
    }

    private void UpdateScribble(Enemy scribble, double dt, bool aggressive)
    {
        var p = Player.Bounds;
        var dx = p.CenterX - scribble.Bounds.CenterX;
        var sees = aggressive && !Player.IsDead && Math.Abs(dx) < Tuning.ScribbleSightRange
                   && Math.Abs(p.CenterY - scribble.Bounds.CenterY) < 120
                   && p.CenterX > scribble.MinX - 40 && p.CenterX < scribble.MaxX + scribble.Width + 40;

        scribble.Special = sees;
        if (sees)
            scribble.VelocityX = Math.Sign(dx) * Tuning.ScribbleChargeSpeed;
        Patrol(scribble, dt, sees ? Tuning.ScribbleChargeSpeed : Tuning.EnemySpeed);
    }

    private void UpdateMoth(Enemy moth, bool aggressive)
    {
        // A lazy figure eight around its home point.
        var t = moth.AnimationTime * 0.8;
        var previousX = moth.X;
        moth.X = moth.HomeX + moth.Range / 2 * Math.Sin(t);
        moth.Y = moth.HomeY + 40 * Math.Sin(2 * t);
        moth.FacingLeft = moth.X < previousX;

        if (!aggressive || Player.IsDead || moth.Timer < Tuning.MothShootInterval)
            return;

        var from = moth.Bounds;
        var to = Player.Bounds;
        var dx = to.CenterX - from.CenterX;
        var dy = to.CenterY - from.CenterY;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance > Tuning.MothSightRange || distance < 1)
            return;

        moth.Timer = 0;
        Projectiles.Add(new Projectile
        {
            Kind = ProjectileKind.Ember,
            X = from.CenterX - Tuning.EmberSize / 2,
            Y = from.CenterY - Tuning.EmberSize / 2,
            Width = Tuning.EmberSize,
            Height = Tuning.EmberSize,
            VelocityX = dx / distance * Tuning.EmberSpeed,
            VelocityY = dy / distance * Tuning.EmberSpeed,
        });
        _events.Add(GameEvent.EmberShot);
    }

    private void UpdateOwl(Enemy owl, bool aggressive)
    {
        owl.FacingLeft = Player.Bounds.CenterX < owl.Bounds.CenterX;

        var cycle = Tuning.OwlRestTime + Tuning.OwlFlapTime;
        var flapping = owl.Timer % cycle > Tuning.OwlRestTime;
        if (flapping && !owl.Special && aggressive)
            _events.Add(GameEvent.OwlGust);
        owl.Special = flapping;
        if (!flapping)
            return;

        var b = owl.Bounds;
        var direction = owl.FacingLeft ? -1 : 1;
        var area = direction > 0
            ? new Box(b.Right, b.CenterY - 100, Tuning.OwlWindReach, 200)
            : new Box(b.Left - Tuning.OwlWindReach, b.CenterY - 100, Tuning.OwlWindReach, 200);
        Gusts.Add((area, direction));

        // Pale streaks fly through the gust.
        if (_random.NextDouble() < 0.5)
        {
            Particles.Add(new Particle
            {
                Kind = ParticleKind.Wind,
                X = direction > 0 ? area.Left : area.Right,
                Y = area.Top + _random.NextDouble() * area.Height,
                VelocityX = direction * (500 + _random.NextDouble() * 250),
                Size = 24 + _random.NextDouble() * 30,
                Life = 0.8,
            });
        }
    }

    /// <summary>An owl's gust pushes the player away from it.</summary>
    private void ApplyWind(double dt)
    {
        foreach (var (area, direction) in Gusts)
        {
            if (area.Overlaps(Player.Bounds))
                MoveHorizontally(direction * Tuning.OwlWindSpeed * dt);
        }
    }

    private void UpdateBurr(Enemy burr, double dt)
    {
        var direction = burr.VelocityX < 0 ? -1 : 1;
        burr.X += direction * Tuning.BurrSpeed * dt;
        burr.FacingLeft = direction < 0;

        // Turn at walls, at the edge of the ground, and at the ends of its range.
        var b = burr.Bounds;
        var ahead = new Box(direction > 0 ? b.Right : b.Left - 2, b.Top, 2, b.Height - 2);
        var footing = new Box(direction > 0 ? b.Right - 4 : b.Left, b.Bottom, 4, 4);
        var blocked = Solids().Any(ahead.Overlaps) || !Solids().Any(footing.Overlaps)
                      || (burr.Range > 0 && (burr.X < burr.MinX || burr.X > burr.MaxX));
        if (blocked)
        {
            burr.X -= direction * Tuning.BurrSpeed * dt;
            burr.VelocityX = -direction * Tuning.BurrSpeed;
        }
    }

    private void UpdateToad(Enemy toad, double dt, bool aggressive)
    {
        toad.SinceLanding += dt;

        if (toad.OnGround)
        {
            var dx = Player.Bounds.CenterX - toad.Bounds.CenterX;
            if (Math.Abs(dx) > 1)
                toad.FacingLeft = dx < 0;

            var inSight = aggressive && !Player.IsDead && Math.Abs(dx) < Tuning.ToadSightRange;
            if (toad.Timer < Tuning.ToadJumpInterval || !inSight)
                return;

            toad.Timer = 0;
            toad.OnGround = false;
            toad.VelocityX = Math.Sign(dx) * Tuning.ToadJumpSpeedX;
            toad.VelocityY = -Tuning.ToadJumpSpeedY;
            return;
        }

        toad.VelocityY = Math.Min(toad.VelocityY + Tuning.Gravity * dt, Tuning.MaxFallSpeed);

        // Sideways, staying inside its home range and stopping at walls.
        var x = toad.X + toad.VelocityX * dt;
        if (toad.Range > 0)
            x = Math.Clamp(x, toad.MinX, toad.MaxX);
        var sideways = new Box(x, toad.Y, toad.Width, toad.Height);
        if (!Solids().Any(sideways.Overlaps))
            toad.X = x;

        toad.Y += toad.VelocityY * dt;
        foreach (var solid in Solids())
        {
            if (!toad.Bounds.Overlaps(solid))
                continue;

            if (toad.VelocityY > 0)
            {
                toad.Y = solid.Top - toad.Height;
                toad.OnGround = true;
                toad.VelocityX = 0;
                toad.SinceLanding = 0;
                SpawnDust(toad.Bounds.CenterX, toad.Bounds.Bottom, 8, 200);
                if (Math.Abs(Player.Bounds.CenterX - toad.Bounds.CenterX) < 700)
                    _events.Add(GameEvent.ToadLanded);
            }
            else
            {
                toad.Y = solid.Bottom;
            }

            toad.VelocityY = 0;
            break;
        }

        if (toad.Y > Level.Height)
            toad.Alive = false;
    }

    /// <summary>Touching enemies: stomping defeats most of them, any other touch costs a life.</summary>
    private bool CheckEnemyContacts(IInput input)
    {
        var p = Player;
        var bounds = p.Bounds;

        foreach (var enemy in Enemies)
        {
            if (!enemy.Alive || !enemy.IsTangible || !bounds.Overlaps(enemy.Bounds))
                continue;

            var isStomp = p.VelocityY > 0 && bounds.Bottom <= enemy.Bounds.Top + Tuning.StompTolerance;
            if (isStomp && enemy.CanBeStomped)
            {
                var held = input.IsDown(GameAction.Jump);
                p.VelocityY = -(held ? Tuning.StompBounceHeld : Tuning.StompBounce);
                p.IsJumping = held;
                p.Squash = -0.2;
                p.FlutterTime = 0;
                DefeatEnemy(enemy, GameEvent.EnemyStomped);
            }
            else if (p.Invulnerable <= 0)
            {
                Kill();
                return true;
            }
        }

        return false;
    }

    private void DefeatEnemy(Enemy enemy, GameEvent how)
    {
        enemy.Alive = false;
        Points += enemy.Points;
        EnemiesDefeated++;
        Effects.Add(new Effect(EffectKind.ScorePopup, enemy.X, enemy.Y, 0.8, enemy.Points.ToString()));
        SpawnDust(enemy.Bounds.CenterX, enemy.Bounds.CenterY, 12, 260);
        _events.Add(how);
    }

    private void SpawnFeather()
    {
        var b = Player.Bounds;
        Particles.Add(new Particle
        {
            Kind = ParticleKind.Feather,
            X = b.CenterX + (_random.NextDouble() - 0.5) * 30,
            Y = b.CenterY,
            VelocityX = (_random.NextDouble() - 0.5) * 80,
            VelocityY = 40 + _random.NextDouble() * 40,
            Gravity = 60,
            Size = 7 + _random.NextDouble() * 4,
            Life = 0.9,
            Spin = (_random.NextDouble() - 0.5) * 400,
        });
    }
}
