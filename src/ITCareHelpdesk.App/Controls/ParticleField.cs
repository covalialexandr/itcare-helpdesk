using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;

namespace ITCareHelpdesk.App.Controls;

// ParticleField — control custom care deseneaza puncte cyan animate pe canvas.
// L-am implementat direct cu Skia in loc de Avalonia.Animation pentru ca animatiile XAML clasice
// ruleaza pe UI thread si gripeaza pe sute de obiecte; aici un singur Render() la 60fps pe canvas
// e mult mai eficient.
public sealed class ParticleField : Control
{
    private readonly List<Particle> _particles = new();
    private readonly Random _rng = new();
    private DispatcherTimer? _timer;
    private DateTime _lastTick;

    private sealed class Particle
    {
        public float X;
        public float Y;
        public float Vx;
        public float Vy;
        public float Radius;
        public float Alpha;
        public float Life;
        public float MaxLife;
    }

    // OnAttachedToVisualTree primeste Avalonia.VisualTreeAttachmentEventArgs (NU "Avalonia.VisualTree.*").
    // Aici pornim timer-ul de tick — il oprim in OnDetachedFromVisualTree ca sa nu lasam ticks "fantoma"
    // pe controale care nu mai sunt in scena.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _lastTick = DateTime.UtcNow;
        // 60 fps este suficient pentru smooth si nu pune presiune pe GPU
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer?.Stop();
        _timer = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var dt  = (float)(now - _lastTick).TotalSeconds;
        _lastTick = now;

        // mentinem ~60 particule active; cream una noua daca scad
        while (_particles.Count < 60)
            _particles.Add(SpawnParticle());

        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
            p.Life -= dt;

            // alpha fade-in la inceput, fade-out la sfarsit — feeling de "respiratie"
            var phase = 1 - p.Life / p.MaxLife;
            p.Alpha = phase < 0.2f
                ? phase / 0.2f
                : phase > 0.8f
                    ? (1 - phase) / 0.2f
                    : 1f;

            if (p.Life <= 0)
                _particles.RemoveAt(i);
        }

        InvalidateVisual();
    }

    private Particle SpawnParticle()
    {
        var w = (float)Math.Max(Bounds.Width, 1);
        var h = (float)Math.Max(Bounds.Height, 1);
        return new Particle
        {
            X       = (float)_rng.NextDouble() * w,
            Y       = h + 10,
            Vx      = ((float)_rng.NextDouble() - 0.5f) * 10,
            Vy      = -10 - (float)_rng.NextDouble() * 30,
            Radius  = 0.5f + (float)_rng.NextDouble() * 2.5f,
            Alpha   = 0,
            MaxLife = 4 + (float)_rng.NextDouble() * 4,
            Life    = 4 + (float)_rng.NextDouble() * 4
        };
    }

    public override void Render(DrawingContext context)
    {
        // Plugam o operatie custom in pipeline-ul de render Avalonia, care primeste lease pe SkCanvas.
        context.Custom(new SkiaParticleOp(new Rect(default, Bounds.Size), _particles));
    }

    // ICustomDrawOperation traieste in Avalonia.Rendering.SceneGraph in Avalonia 11.x
    // (NU in Avalonia.Rendering, cum era in 0.10.x — schimbarea de namespace e un breaking change cunoscut).
    private sealed class SkiaParticleOp : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly List<Particle> _particles;

        public SkiaParticleOp(Rect bounds, List<Particle> particles)
        {
            _bounds = bounds;
            _particles = particles;
        }

        public Rect Bounds => _bounds;

        // Particulele nu reactioneaza la mouse — return false pe HitTest scoate explicit interactivitatea.
        public bool HitTest(Point p) => false;

        // Equals returneaza intotdeauna false ca Avalonia sa nu cache-uiasca operatia intre frame-uri
        // (ar fi gresit pentru ca _particles se schimba la fiecare tick).
        public bool Equals(ICustomDrawOperation? other) => false;

        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            // Avalonia 11.x are TryGetFeature non-generic; intoarce object si trebuie cast manual.
            // Vechiul TryGetFeature<T>() din 0.10.x a disparut.
            var leaseFeature = context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) as ISkiaSharpApiLeaseFeature;
            if (leaseFeature is null) return;

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;

            using var paint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                // Blur subtle ca particulele sa "respire" — fara filter ar parea solide si plastice
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 1.4f)
            };

            foreach (var p in _particles)
            {
                // Cyan #43BAFF cu alpha modulata din viata particulei
                var alpha = (byte)Math.Clamp(p.Alpha * 200, 0, 255);
                paint.Color = new SKColor(0x43, 0xBA, 0xFF, alpha);
                canvas.DrawCircle(p.X, p.Y, p.Radius, paint);
            }
        }
    }
}
