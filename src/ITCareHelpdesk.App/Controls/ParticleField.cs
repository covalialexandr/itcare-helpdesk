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

// ParticleField = control custom care deseneaza particule animate pe fundal.
// Practic acesta este efectul cu "bulele" / punctele luminoase care plutesc pe ecran.
// Am folosit Skia direct pentru performanta mai buna fata de animatii XAML clasice.
public sealed class ParticleField : Control
{
    // Lista cu toate particulele active pe ecran
    private readonly List<Particle> _particles = new();

    // Random folosit pentru pozitii, viteze si dimensiuni random
    private readonly Random _rng = new();

    // Timer care actualizeaza animatia la fiecare frame
    private DispatcherTimer? _timer;

    // Ultimul moment de update folosit pentru calcul delta-time
    private DateTime _lastTick;

    // Clasa interna simpla care reprezinta o singura particula
    private sealed class Particle
    {
        // Pozitia pe axa X
        public float X;

        // Pozitia pe axa Y
        public float Y;

        // Viteza pe X
        public float Vx;

        // Viteza pe Y
        public float Vy;

        // Raza cercului desenat
        public float Radius;

        // Transparenta curenta
        public float Alpha;

        // Viata ramasa
        public float Life;

        // Durata maxima de viata
        public float MaxLife;
    }

    // Cand controlul este atasat in UI pornim animatia
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Salvam timpul initial
        _lastTick = DateTime.UtcNow;

        // Timer ~60 FPS
        // 16ms = aproximativ 60 cadre pe secunda
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };

        // La fiecare tick apelam metoda OnTick
        _timer.Tick += OnTick;

        // Pornim timer-ul
        _timer.Start();
    }

    // Cand controlul dispare din UI oprim timer-ul
    // foarte important ca sa nu consume resurse inutil
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer?.Stop();
        _timer = null;

        base.OnDetachedFromVisualTree(e);
    }

    // Update pentru animatie
    private void OnTick(object? sender, EventArgs e)
    {
        // Timpul actual
        var now = DateTime.UtcNow;

        // Delta time = cate secunde au trecut de la ultimul frame
        var dt = (float)(now - _lastTick).TotalSeconds;

        // Actualizam timpul precedent
        _lastTick = now;

        // Pastram aproximativ 60 particule active
        // Daca scad sub 60 generam unele noi
        while (_particles.Count < 60)
            _particles.Add(SpawnParticle());

        // Iteram invers ca sa putem sterge elemente fara probleme
        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];

            // Miscare pe baza vitezei
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;

            // Scadem durata de viata
            p.Life -= dt;

            // Calculam faza vietii particulei
            // 0 = abia nascuta
            // 1 = aproape moarta
            var phase = 1 - p.Life / p.MaxLife;

            // Fade-in la inceput si fade-out la final
            // pentru efect smooth
            p.Alpha = phase < 0.2f
                ? phase / 0.2f
                : phase > 0.8f
                    ? (1 - phase) / 0.2f
                    : 1f;

            // Daca particula a murit o eliminam
            if (p.Life <= 0)
                _particles.RemoveAt(i);
        }

        // Fortam redraw-ul controlului
        InvalidateVisual();
    }

    // Creeaza o particula noua random
    private Particle SpawnParticle()
    {
        // Latimea controlului
        var w = (float)Math.Max(Bounds.Width, 1);

        // Inaltimea controlului
        var h = (float)Math.Max(Bounds.Height, 1);

        return new Particle
        {
            // Pozitie random pe latime
            X = (float)_rng.NextDouble() * w,

            // Porneste de jos
            Y = h + 10,

            // Drift mic stanga-dreapta
            Vx = ((float)_rng.NextDouble() - 0.5f) * 10,

            // Miscare in sus
            Vy = -10 - (float)_rng.NextDouble() * 30,

            // Dimensiune random
            Radius = 0.5f + (float)_rng.NextDouble() * 2.5f,

            // Initial transparenta 0
            Alpha = 0,

            // Durata maxima random
            MaxLife = 4 + (float)_rng.NextDouble() * 4,

            // Viata initiala
            Life = 4 + (float)_rng.NextDouble() * 4
        };
    }

    // Render-ul controlului
    public override void Render(DrawingContext context)
    {
        // Cream snapshot al listei
        // foarte important pentru thread safety
        // deoarece render-ul poate rula simultan cu update-ul
        var snapshot = _particles.ToArray();

        // Trimitem snapshot-ul catre custom draw operation
        context.Custom(new SkiaParticleOp(
            new Rect(default, Bounds.Size),
            snapshot));
    }

    // Clasa responsabila de desenarea efectiva cu Skia
    private sealed class SkiaParticleOp : ICustomDrawOperation
    {
        // Limitele controlului
        private readonly Rect _bounds;

        // Snapshot readonly cu particulele
        private readonly Particle[] _particles;

        public SkiaParticleOp(Rect bounds, Particle[] particles)
        {
            _bounds = bounds;
            _particles = particles;
        }

        // Zona desenata
        public Rect Bounds => _bounds;

        // Nu interactioneaza cu mouse-ul
        public bool HitTest(Point p) => false;

        // Return false ca Avalonia sa nu cache-uiasca desenul
        // deoarece particulele se schimba constant
        public bool Equals(ICustomDrawOperation? other) => false;

        // Nimic de eliberat
        public void Dispose() { }

        // Render efectiv
        public void Render(ImmediateDrawingContext context)
        {
            // Obtinem acces la engine-ul Skia
            var leaseFeature =
                context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature))
                as ISkiaSharpApiLeaseFeature;

            // Daca Skia nu este disponibil iesim
            if (leaseFeature is null)
                return;

            // Lease pentru canvas
            using var lease = leaseFeature.Lease();

            // Canvas Skia
            var canvas = lease.SkCanvas;

            // Paint reutilizat pentru toate particulele
            using var paint = new SKPaint
            {
                // Anti alias pentru cercuri smooth
                IsAntialias = true,

                // Desen fill
                Style = SKPaintStyle.Fill,

                // Blur pentru glow effect
                MaskFilter = SKMaskFilter.CreateBlur(
                    SKBlurStyle.Normal,
                    1.4f)
            };

            // Desenam fiecare particula
            foreach (var p in _particles)
            {
                // Calculam alpha final
                var alpha = (byte)Math.Clamp(
                    p.Alpha * 200,
                    0,
                    255);

                // Culoare cyan cu transparenta variabila
                paint.Color = new SKColor(
                    0x43,
                    0xBA,
                    0xFF,
                    alpha);

                // Desenam cercul
                canvas.DrawCircle(
                    p.X,
                    p.Y,
                    p.Radius,
                    paint);
            }
        }
    }
}
