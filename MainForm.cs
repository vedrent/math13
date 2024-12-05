using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Net.Sockets;
using System.Windows.Forms;

namespace math13
{
    public class SoundPlayer
    {
        private WaveOutEvent outputDevice;
        private AudioFileReader audioFile;

        public void Play(string filePath, bool loop = false)
        {
            //Stop(); // Останавливаем текущий звук, если проигрывается

            var reader = new AudioFileReader(filePath);
            audioFile = reader;
            outputDevice = new WaveOutEvent();


            outputDevice.Init(audioFile);
            outputDevice.Play();
        }

        public void Stop()
        {
            outputDevice?.Stop();
            outputDevice?.Dispose();
            audioFile?.Dispose();

            outputDevice = null;
            audioFile = null;
        }

        // Вложенный класс для зацикливания звука
        private class LoopStream : WaveStream
        {
            private readonly WaveStream sourceStream;

            public LoopStream(WaveStream sourceStream)
            {
                this.sourceStream = sourceStream;
                this.EnableLooping = true;
            }

            public bool EnableLooping { get; set; }

            public override WaveFormat WaveFormat => sourceStream.WaveFormat;

            public override long Length => sourceStream.Length;

            public override long Position
            {
                get => sourceStream.Position;
                set => sourceStream.Position = value;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = sourceStream.Read(buffer, offset, count);
                if (read == 0 && EnableLooping)
                {
                    sourceStream.Position = 0;
                    read = sourceStream.Read(buffer, offset, count);
                }
                return read;
            }
        }
    }

    public partial class MainForm : Form
    {
        private System.Windows.Forms.Timer gameTimer = new System.Windows.Forms.Timer();
        private Spaceship spaceship;
        private List<Rocket> rockets = new List<Rocket>();
        private List<Comet> comets = new List<Comet>();
        private List<Explosion> explosions = new List<Explosion>();
        private List<Item> items = new List<Item>();
        private Random random = new Random();
        private HashSet<Keys> pressedKeys = new HashSet<Keys>();
        private SoundPlayer soundPlayer = new SoundPlayer();

        private int score = 0;
        private bool gameOver = false;
        private int deathTimer = 0;
        private int rocketCount = 2;
        private int explosionRadius = 50;
        private int cometSpawnRate = 3;
        private int cometSpeedX = 2;
        private int cometSpeedY = 3;

        private Image spaceshipImage;
        private Image rocketImage;
        private Image cometImage;
        private Image goldCometImage;
        private Image explosionImage;
        private AnimatedGif explosionAnimation;
        private List<Image> explosionFrames;
        private List<int> explosionDelays;

        private Image backgroundImage;
        private int backgroundOffset = 0; // Смещение по вертикали
        private const int BackgroundSpeed = 2;

        private Image rocketBoostImage;
        private Image explosionBoostImage;

        private string soundtrackSoundPath;
        private string explosionSoundPath;
        private string spaceshipExplosionSoundPath;
        private string shootSoundPath;
        private string itemSoundPath;

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
            goldCometImage = Image.FromFile("C:\\_coding\\progmath\\math13\\Images\\gold_comet.png");
            explosionImage = Image.FromFile("C:\\_coding\\progmath\\math13\\Images\\explosion.gif");
            LoadExplosionFrames(explosionImage);

            backgroundImage = Image.FromFile("C:\\_coding\\progmath\\math13\\Images\\background.png");

            rocketBoostImage = Image.FromFile("C:\\_coding\\progmath\\math13\\Images\\rocket_boost.png");
            explosionBoostImage = Image.FromFile("C:\\_coding\\progmath\\math13\\Images\\explosion_boost.png");

            soundtrackSoundPath = "C:\\_coding\\progmath\\math13\\Sounds\\soundtrack.mp3";
            explosionSoundPath = "C:\\_coding\\progmath\\math13\\Sounds\\explosion.mp3";
            spaceshipExplosionSoundPath = "C:\\_coding\\progmath\\math13\\Sounds\\spaceship_explosion.mp3";
            shootSoundPath = "C:\\_coding\\progmath\\math13\\Sounds\\fire.mp3";
            itemSoundPath = "C:\\_coding\\progmath\\math13\\Sounds\\item.mp3";

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

            soundPlayer.Play(soundtrackSoundPath);
        }

        private void GameTick(object sender, EventArgs e)
        {
            if (gameOver && deathTimer == 0) return;

            switch (score)
            {
                case >= 200 and < 400:
                    cometSpawnRate = 5;
                    cometSpeedX = 3;
                    cometSpeedY = 4;
                    break;
                case >= 400 and < 600:
                    cometSpawnRate = 7;
                    cometSpeedY = 5;
                    break;
                case >= 600 and < 800:
                    cometSpawnRate = 9;
                    cometSpeedX = 5;
                    cometSpeedY = 6;
                    break;
                case >= 800 and < 1000:
                    cometSpawnRate = 11;
                    cometSpeedY = 7;
                    break;
                case >= 1000:
                    cometSpawnRate = 13;
                    cometSpeedX = 7;
                    cometSpeedY = 8;
                    break;

            }

            backgroundOffset += BackgroundSpeed;

            // Если фон полностью прокручивается, сбрасываем смещение
            if (backgroundOffset >= backgroundImage.Height)
            {
                backgroundOffset = 0;
            }

            // Обновление положения объектов
            spaceship.Move(ClientSize);
            rockets.ForEach(r => r.Move());
            comets.ForEach(c => c.Move());
            items.ForEach(i => i.Fall());

            // Удаление ракет и комет, вышедших за границы
            rockets.RemoveAll(r => r.IsOutOfBounds(ClientSize));
            comets.RemoveAll(c => c.IsOutOfBounds(ClientSize));
            items.RemoveAll(i => i.IsOutOfBounds(ClientSize));

            // Генерация комет
            if (random.Next(0, 100) < cometSpawnRate) // шанс появления новой кометы
            {
                int x;
                int y;
                int size = new Random().Next(60, 150);
                if (random.NextDouble() < 0.5)
                {
                    x = random.Next(-size, ClientSize.Width);
                    y = -size;
                } else if (random.NextDouble() < 0.75)
                {
                    x = -size;
                    y = random.Next(-size, ClientSize.Height / 2);
                }
                else
                {
                    x = ClientSize.Width;
                    y = random.Next(-size, ClientSize.Height / 2);
                }
                if (random.Next(0, 100) < 5) comets.Add(new Comet(x, y, 1, cometSpeedX, cometSpeedY, size));
                else comets.Add(new Comet(x, y, 0, cometSpeedX, cometSpeedY, size));
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
                            soundPlayer.Play(explosionSoundPath);
                            foreach (var hitComet in hitComets)
                            {
                                hitComet.OnDestroyed(items);
                                if (hitComet.Type == 0) score += 10;
                                else if (hitComet.Type == 1) score += 50;
                                comets.Remove(hitComet); // Уничтожаем все кометы в радиусе
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
                    deathTimer = 130;
                    spaceship.Direction = new Point(spaceship.Direction.X / 3, spaceship.Direction.Y / 3);
                    explosions.Add(new Explosion(new Rectangle(spaceship.X, spaceship.Y, 50, 50), new AnimatedGif(explosionFrames, explosionDelays)));
                    soundPlayer.Play(explosionSoundPath);
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

            foreach (var item in items.ToList())
            {
                if (item.Bounds.IntersectsWith(spaceship.Bounds)) // Если предмет касается корабля
                {
                    item.IsCollected = true;
                    HandleItemCollection(item);
                    items.Remove(item); // Убираем предмет
                }
            }

            if (gameOver)
            {
                if (deathTimer > 65)
                {
                    deathTimer--;
                    if (deathTimer % 4 == 0)
                    {
                        explosions.Add(new Explosion(new Rectangle(spaceship.X, spaceship.Y, 50, 50), new AnimatedGif(explosionFrames, explosionDelays)));
                    }
                }
                else if (deathTimer > 1) {
                    deathTimer--;
                    
                }
                else
                {
                    deathTimer--;
                    gameTimer.Stop();
                    MessageBox.Show($"Игра окончена! Ваш счет: {score}");
                }

                if (deathTimer == 85)
                {
                    soundPlayer.Play(spaceshipExplosionSoundPath);
                }

                if (deathTimer == 65)
                {
                    spaceship.Direction = new Point(0, 0);
                    explosions.Add(new Explosion(new Rectangle(spaceship.X - 50, spaceship.Y - 50, 150, 150), new AnimatedGif(explosionFrames, explosionDelays)));
                }

            }

            Invalidate(); // Перерисовка
        }

        private void HandleItemCollection(Item item)
        {
            soundPlayer.Play(itemSoundPath);
            if (item.Type == 0)
            {
                rocketCount ++;
            }
            else if (item.Type == 1)
            {
                explosionRadius *= 2; 
                System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 5000 }; // 5 секунд
                timer.Tick += (s, e) =>
                {
                    explosionRadius /= 2; // Восстанавливаем радиус
                    timer.Stop();
                };
                timer.Start();
            }
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

            if (e.KeyCode == Keys.Space && rockets.Count < rocketCount)
            {
                rockets.Add(new Rocket(spaceship.X + 20, spaceship.Y - 10, explosionRadius));
                soundPlayer.Play(shootSoundPath);
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

            g.DrawImage(backgroundImage, new Rectangle(0, backgroundOffset, ClientSize.Width, backgroundImage.Height));
            g.DrawImage(backgroundImage, new Rectangle(0, backgroundOffset - backgroundImage.Height, ClientSize.Width, backgroundImage.Height));


            // Отрисовка звездолета
            if (deathTimer == 0 || deathTimer > 50)
            {
                spaceship.Draw(g, spaceshipImage);
            }

            // Отрисовка ракет
            foreach (var rocket in rockets)
            {
                rocket.Draw(g, rocketImage);
            }

            // Отрисовка комет
            foreach (var comet in comets)
            {
                if (comet.Type == 0) comet.Draw(g, cometImage);
                else if (comet.Type == 1) comet.Draw(g, goldCometImage);
            }

            foreach (var explosion in explosions)
            {
                explosion.Draw(g);
            }

            foreach (var item in items)
            {
                if (item.Type == 0) item.Draw(g, rocketBoostImage);
                else if (item.Type == 1) item.Draw(g, explosionBoostImage);
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
        public int ExplosionRadius { get; private set; }

        public Rocket(int x, int y, int explosionRadius)
        {
            X = x;
            Y = y;
            ExplosionRadius = explosionRadius;
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
        public int SizeX { get; private set; }
        public int SizeY { get; private set; }
        public Rectangle Bounds => new Rectangle(X, Y, SizeX, SizeY);
        public int SpeedX { get; private set; }
        public int SpeedY { get; private set; }
        public int Type { get; private set; }


        public Comet(int x, int y, int type, int speedX, int speedY, int size)
        {
            X = x;
            Y = y;
            SizeX = SizeY = size;
            Type = type;
            SpeedX = new Random().Next(-speedX, speedX);
            SpeedY = new Random().Next(speedY, speedY + 2);
        }

        public void Move()
        {
            X += SpeedX;
            Y += SpeedY; // Движение вниз
        }

        public bool IsOutOfBounds(Size clientSize)
        {
            return Y > clientSize.Height || X > clientSize.Width + 10 || X < -SizeX - 10;
        }

        public void Draw(Graphics g, Image cometImage)
        {
            g.DrawImage(cometImage, Bounds);
        }

        public void OnDestroyed(List<Item> items)
        {
            Random rand = new Random();
            // Шанс выпадения предмета, например, 30%
            if (rand.NextDouble() < 0.3)
            {
                int itemType = rand.Next(0, 2);
                items.Add(new Item(X, Y, itemType)); // Предмет падает с центра кометы
            }
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

    public class Item
    {
        public Rectangle Bounds { get; private set; }
        public int Type { get; private set; }
        public bool IsCollected { get; set; }

        private int speed = 3; // Скорость падения предметов

        public Item(int x, int y, int type)
        {
            Bounds = new Rectangle(x, y, 30, 30); // Размер предмета
            Type = type;
        }

        public void Fall()
        {
            Bounds = new Rectangle(Bounds.X, Bounds.Y + speed, Bounds.Width, Bounds.Height); // Падение вниз
        }

        public bool IsOutOfBounds(Size clientSize)
        {
            return Bounds.Y > clientSize.Height;
        }

        public void Draw(Graphics g, Image itemImage)
        {
            g.DrawImage(itemImage, Bounds);
        }
    }
}
