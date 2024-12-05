using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Net.Sockets;
using System.Windows.Forms;

namespace math13
{
    public partial class MainForm : Form
    {
        private System.Windows.Forms.Timer gameTimer = new System.Windows.Forms.Timer();
        private Spaceship spaceship;
        private List<Rocket> rockets = new List<Rocket>();
        private List<Comet> comets = new List<Comet>();
        private List<Explosion> explosions = new List<Explosion>();
        private Random random = new Random();
        private int score = 0;
        private bool gameOver = false;
        private int deathTimer = 0;
        private HashSet<Keys> pressedKeys = new HashSet<Keys>();

        private Image spaceshipImage;
        private Image rocketImage;
        private Image cometImage;
        private Image explosionImage;
        private AnimatedGif explosionAnimation;
        private List<Image> explosionFrames;
        private List<int> explosionDelays;

        private void LoadExplosionFrames(Image gif)
        {
            explosionFrames = new List<Image>();
            explosionDelays = new List<int>();

            var dimension = new FrameDimension(gif.FrameDimensionsList[0]);
            int frameCount = gif.GetFrameCount(dimension);

            for (int i = 0; i < frameCount; i+=4)
            {
                gif.SelectActiveFrame(dimension, i);
                explosionFrames.Add(new Bitmap(gif));

                // Получаем задержки кадров
                var item = gif.GetPropertyItem(0x5100); // PropertyTagFrameDelay
                int delay = BitConverter.ToInt32(item.Value, i * 4) * 10; // Время в миллисекундах
                explosionDelays.Add(delay);
            }
        }

        public MainForm()
        {
            spaceshipImage = Image.FromFile("C:\\_coding\\progmath\\math13\\Images\\spaceship.png");
            rocketImage = Image.FromFile("C:\\_coding\\progmath\\math13\\Images\\rocket.png");
            cometImage = Image.FromFile("C:\\_coding\\progmath\\math13\\Images\\comet.png");
            explosionImage = Image.FromFile("C:\\_coding\\progmath\\math13\\Images\\explosion.gif");
            LoadExplosionFrames(explosionImage);

            //InitializeComponent();
            InitGame();
        }

        private void InitGame()
        {
            this.Width = 800;
            this.Height = 600;
            this.DoubleBuffered = true;

            spaceship = new Spaceship(ClientSize.Width / 2 - 25, ClientSize.Height - 60);

            gameTimer.Interval = 20;
            gameTimer.Tick += GameTick;
            gameTimer.Start();

            this.KeyDown += MainForm_KeyDown;
            this.KeyUp += MainForm_KeyUp;
            this.Paint += MainForm_Paint;
        }

        private void GameTick(object sender, EventArgs e)
        {
            if (gameOver && deathTimer == 0) return;

            // Обновление положения объектов
            spaceship.Move(ClientSize);
            rockets.ForEach(r => r.Move());
            comets.ForEach(c => c.Move());

            // Удаление ракет и комет, вышедших за границы
            rockets.RemoveAll(r => r.IsOutOfBounds(ClientSize));
            comets.RemoveAll(c => c.IsOutOfBounds(ClientSize));

            // Генерация комет
            if (random.Next(0, 100) < 3) // 3% шанс появления новой кометы
            {
                int x = random.Next(-70, ClientSize.Width);
                comets.Add(new Comet(x, -70));
            }

            // Проверка столкновений
            foreach (var rocket in rockets.ToList())
            {
                foreach (var comet in comets.ToList())
                {
                    if (rocket.Bounds.IntersectsWith(comet.Bounds))
                    {
                        var hitComets = comets.Where(c => rocket.ExplosionArea.IntersectsWith(c.Bounds)).ToList();

                        if (hitComets.Count > 0)
                        {
                            rockets.Remove(rocket); // Удаляем ракету после взрыва
                            explosions.Add(new Explosion(rocket.ExplosionArea, new AnimatedGif(explosionFrames, explosionDelays)));
                            foreach (var hitComet in hitComets)
                            {
                                comets.Remove(hitComet); // Уничтожаем все кометы в радиусе
                                score += 10;         // Начисляем очки
                            }
                        }
                    }
                }
            }

            foreach (var comet in comets)
            {
                if (spaceship.Bounds.IntersectsWith(comet.Bounds) && !gameOver)
                {
                    gameOver = true;
                    deathTimer = 65;
                    spaceship.Direction = new Point(spaceship.Direction.X / 3, spaceship.Direction.Y / 3);
                    explosions.Add(new Explosion(new Rectangle(spaceship.X, spaceship.Y, 50, 50), new AnimatedGif(explosionFrames, explosionDelays)));
                }
            }

            foreach (var explosion in explosions.ToList())
            {
                explosion.Update();
                if (explosion.IsFinished)
                {
                    explosions.Remove(explosion);
                }
            }

            if (gameOver)
            {
                if (deathTimer > 1)
                {
                    deathTimer--;
                    if (deathTimer % 4 == 0)
                    {
                        explosions.Add(new Explosion(new Rectangle(spaceship.X, spaceship.Y, 50, 50), new AnimatedGif(explosionFrames, explosionDelays)));
                    }
                }
                else
                {
                    deathTimer--;
                    gameTimer.Stop();
                    MessageBox.Show($"Игра окончена! Ваш счет: {score}");
                }

            }

            Invalidate(); // Перерисовка
        }

        private void OnFrameChanged(object sender, EventArgs e)
        {
            Invalidate(); // Перерисовываем окно для отображения нового кадра
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (gameOver) return;

            pressedKeys.Add(e.KeyCode);
            UpdateSpaceshipDirection();

            if (e.KeyCode == Keys.Space && rockets.Count < 3)
            {
                rockets.Add(new Rocket(spaceship.X + 20, spaceship.Y - 10));
            }
        }

        private void MainForm_KeyUp(object sender, KeyEventArgs e)
        {
            if (gameOver) return;

            pressedKeys.Remove(e.KeyCode);
            UpdateSpaceshipDirection();
        }

        private void UpdateSpaceshipDirection()
        {
            int dx = 0, dy = 0;

            if (pressedKeys.Contains(Keys.W)) dy -= 10;
            if (pressedKeys.Contains(Keys.S)) dy += 10;
            if (pressedKeys.Contains(Keys.A)) dx -= 10;
            if (pressedKeys.Contains(Keys.D)) dx += 10;

            spaceship.Direction = new Point(dx, dy);
        }

        private void MainForm_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // Отрисовка звездолета
            spaceship.Draw(g, spaceshipImage);

            // Отрисовка ракет
            foreach (var rocket in rockets)
            {
                rocket.Draw(g, rocketImage);
            }

            // Отрисовка комет
            foreach (var comet in comets)
            {
                comet.Draw(g, cometImage);
            }

            foreach (var explosion in explosions)
            {
                explosion.Draw(g);
            }

            // Отображение очков
            g.DrawString($"Очки: {score}", new Font("Arial", 16), Brushes.White, 10, 10);
        }
    }

    public class Spaceship
    {
        public int X { get; private set; }
        public int Y { get; private set; }
        public Point Direction { get; set; } = Point.Empty;
        public Rectangle Bounds => new Rectangle(X, Y, 50, 50);

        public Spaceship(int x, int y)
        {
            X = x;
            Y = y;
        }

        public void Move(Size clientSize)
        {
            X += Direction.X;
            Y += Direction.Y;

            // Ограничение движения внутри окна
            X = Math.Max(0, Math.Min(clientSize.Width - 50, X));
            Y = Math.Max(0, Math.Min(clientSize.Height - 50, Y));
        }

        public void Draw(Graphics g, Image spaceshipImage)
        {
            g.DrawImage(spaceshipImage, Bounds);
        }
    }

    public class Rocket
    {
        public int X { get; private set; }
        public int Y { get; private set; }
        public Rectangle Bounds => new Rectangle(X, Y, 15, 40);
        public int ExplosionRadius { get; } = 50;

        public Rocket(int x, int y)
        {
            X = x;
            Y = y;
        }

        public void Move()
        {
            Y -= 10; // Движение вверх
        }

        public bool IsOutOfBounds(Size clientSize)
        {
            return Y < 0;
        }

        public Rectangle ExplosionArea => new Rectangle(
            X - ExplosionRadius,
            Y - ExplosionRadius,
            ExplosionRadius * 2,
            ExplosionRadius * 2
        );


        public void Draw(Graphics g, Image rocketImage)
        {
            g.DrawImage(rocketImage, Bounds);
        }
    }

    public class Comet
    {
        public int X { get; private set; }
        public int Y { get; private set; }
        public Rectangle Bounds => new Rectangle(X, Y, 70, 70);
        public int SpeedX { get; private set; }
        public int SpeedY { get; private set; }


        public Comet(int x, int y)
        {
            X = x;
            Y = y;
            SpeedX = new Random().Next(-2, 2);
            SpeedY = new Random().Next(4, 6);
        }

        public void Move()
        {
            X += SpeedX;
            Y += SpeedY; // Движение вниз
        }

        public bool IsOutOfBounds(Size clientSize)
        {
            return Y > clientSize.Height || X > clientSize.Width || X < -70;
        }

        public void Draw(Graphics g, Image cometImage)
        {
            g.DrawImage(cometImage, Bounds);
        }
    }

    public class Explosion
    {
        public Rectangle Area { get; }
        private AnimatedGif animation;
        public bool IsFinished => animation.IsLastFrame();

        public Explosion(Rectangle area, AnimatedGif gif)
        {
            Area = area;
            animation = gif;
        }

        public void Update()
        {
            animation.Update();
        }

        public void Draw(Graphics g)
        {
            g.DrawImage(animation.GetCurrentFrame(), Area);
        }
    }

    public class AnimatedGif
    {
        private List<Image> frames;
        private List<int> delays; // Задержки между кадрами в миллисекундах
        private int currentFrame = 0;
        private int frameTimer = 0;

        public AnimatedGif(List<Image> frames, List<int> delays)
        {
            // Извлечение кадров и задержек
            this.frames = frames;
            this.delays = delays;
        }

        public void Update()
        {
            frameTimer += 16; // Обновляем таймер (предполагаем 60 FPS, 16 мс на кадр)
            if (frameTimer >= delays[currentFrame])
            {
                frameTimer = 0;
                currentFrame = (currentFrame + 1) % frames.Count;
            }
        }

        public Image GetCurrentFrame()
        {
            return frames[currentFrame];
        }

        public bool IsLastFrame()
        {
            return currentFrame == frames.Count - 1;
        }

        public void Reset()
        {
            currentFrame = 0;
            frameTimer = 0;
        }
    }
}
