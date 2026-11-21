using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using TechStore.Models;
using TechStore.Options;

namespace TechStore.Data;

public static class DbSeeder
{
    public const string AdminRole = "Admin";
    public const string CustomerRole = "Customer";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var db = provider.GetRequiredService<AppDbContext>();
        var env = provider.GetRequiredService<IWebHostEnvironment>();
        var dbOptions = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        if (dbOptions.AutoMigrate || env.IsDevelopment())
        {
            if (db.Database.IsNpgsql() && !HasNpgsqlMigrations(db))
            {
                logger.LogInformation(
                    "Bundled EF Core migrations target SQLite; ensuring native PostgreSQL schema via EnsureCreatedAsync.");
                await db.Database.EnsureCreatedAsync();
            }
            else
            {
                await db.Database.MigrateAsync();
            }
        }

        await SeedRolesAsync(provider, logger);
        await SeedAdminUserAsync(provider, env, logger);
        await SeedCatalogAsync(db, logger);
    }

    private static bool HasNpgsqlMigrations(AppDbContext db)
    {
        var migrationsAssembly = db.GetService<IMigrationsAssembly>();
        var snapshotModel = migrationsAssembly.ModelSnapshot?.Model;
        return snapshotModel?.FindAnnotation("Npgsql:ValueGenerationStrategy") is not null;
    }

    private static async Task SeedRolesAsync(IServiceProvider provider, ILogger logger)
    {
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in new[] { AdminRole, CustomerRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded)
                {
                    logger.LogError("Failed creating role {Role}: {Errors}", role,
                        string.Join("; ", result.Errors.Select(e => e.Description)));
                }
            }
        }
    }

    private static async Task SeedAdminUserAsync(IServiceProvider provider, IWebHostEnvironment env, ILogger logger)
    {
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var adminOptions = provider.GetRequiredService<IOptions<SeedAdminOptions>>().Value;

        var email = string.IsNullOrWhiteSpace(adminOptions.Email)
            ? "admin@techstore.local"
            : adminOptions.Email.Trim();

        var existingAdmin = await userManager.FindByEmailAsync(email);
        if (existingAdmin is not null)
        {
            if (!await userManager.IsInRoleAsync(existingAdmin, AdminRole))
            {
                await userManager.AddToRoleAsync(existingAdmin, AdminRole);
            }

            return;
        }

        var password = adminOptions.Password;
        var generatedInDev = false;

        if (string.IsNullOrWhiteSpace(password))
        {
            if (!env.IsDevelopment())
            {
                logger.LogWarning(
                    "SeedAdmin:Password is not configured in {Environment}. Skipping initial Admin creation. " +
                    "Set SeedAdmin__Password via environment variables to bootstrap the Admin account.",
                    env.EnvironmentName);
                return;
            }

            password = GenerateSecurePassword();
            generatedInDev = true;
        }

        var adminUser = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = string.IsNullOrWhiteSpace(adminOptions.FullName)
                ? "TechStore Administrator"
                : adminOptions.FullName.Trim(),
            PreferredLanguage = "en",
            DefaultCity = "Baku",
            DefaultCountry = "Azerbaijan",
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(adminUser, password);
        if (!createResult.Succeeded)
        {
            logger.LogError("Failed to seed Admin user {Email}: {Errors}", email,
                string.Join("; ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(adminUser, AdminRole);

        if (generatedInDev)
        {
            logger.LogWarning(
                "========================================================================\n" +
                "  [TechStore Dev Bootstrap] Created default Admin account:\n" +
                "  Email:    {Email}\n" +
                "  Password: {Password}\n" +
                "  Tip: To use a fixed local password, run:\n" +
                "       dotnet user-secrets set \"SeedAdmin:Password\" \"<YourPassword>\"\n" +
                "========================================================================",
                email, password);
        }
        else
        {
            logger.LogInformation("Seeded Admin account {Email} from configured secrets.", email);
        }
    }

    private static string GenerateSecurePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%&*-_";
        const string all = upper + lower + digits + symbols;

        var chars = new char[16];
        chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        chars[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];

        for (var i = 4; i < chars.Length; i++)
        {
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }

    private static async Task SeedCatalogAsync(AppDbContext db, ILogger logger)
    {
        if (await db.Categories.AnyAsync())
        {
            return;
        }

        var categories = new List<Category>
        {
            new()
            {
                Slug = "laptops",
                NameEn = "Laptops",
                NameRu = "Ноутбуки",
                NameAz = "Noutbuklar",
                DescriptionEn = "Ultrabooks, creator workstations, and OLED performance laptops.",
                DescriptionRu = "Ультрабуки, рабочие станции для креаторов и игровые OLED-ноутбуки.",
                DescriptionAz = "Ultrabuklar, yaradıcı iş stansiyaları və OLED performans noutbukları.",
                IconName = "laptop",
                DisplayOrder = 1,
                IsActive = true
            },
            new()
            {
                Slug = "components",
                NameEn = "PC Components",
                NameRu = "Комплектующие ПК",
                NameAz = "Kompüter hissələri",
                DescriptionEn = "Next-gen GPUs, multi-core processors, motherboards, and ATX 3.1 power supplies.",
                DescriptionRu = "Видеокарты нового поколения, многоядерные процессоры, материнские платы и блоки питания ATX 3.1.",
                DescriptionAz = "Yeni nəsil videokartlar, çoxnüvəli prosessorlar, ana platalar və ATX 3.1 qida blokları.",
                IconName = "cpu",
                DisplayOrder = 2,
                IsActive = true
            },
            new()
            {
                Slug = "monitors",
                NameEn = "Monitors",
                NameRu = "Мониторы",
                NameAz = "Monitorlar",
                DescriptionEn = "High-refresh OLED gaming panels and factory-calibrated 4K studio displays.",
                DescriptionRu = "Быстрые OLED-мониторы и калиброванные 4K-дисплеи для студийной работы.",
                DescriptionAz = "Yüksək tezlikli OLED oyun panelləri və fabrik kalibrli 4K studiya monitorları.",
                IconName = "monitor",
                DisplayOrder = 3,
                IsActive = true
            },
            new()
            {
                Slug = "peripherals",
                NameEn = "Peripherals",
                NameRu = "Периферия",
                NameAz = "Periferiya",
                DescriptionEn = "CNC mechanical keyboards, ultralight esports mice, and studio ANC audio.",
                DescriptionRu = "Механические клавиатуры из алюминия, ультралёгкие мыши и студийные наушники с ANC.",
                DescriptionAz = "CNC mexaniki klaviaturalar, ultra-yüngül siçanlar və ANC studiya qulaqlıqları.",
                IconName = "keyboard",
                DisplayOrder = 4,
                IsActive = true
            },
            new()
            {
                Slug = "storage-memory",
                NameEn = "Storage & Memory",
                NameRu = "Память и накопители",
                NameAz = "Yaddaş və disklər",
                DescriptionEn = "PCIe NVMe SSDs and low-latency DDR5 memory kits.",
                DescriptionRu = "Скоростные PCIe NVMe SSD и комплекты оперативной памяти DDR5 с низкими таймингами.",
                DescriptionAz = "Sürətli PCIe NVMe SSD disklər və aşağı gecikməli DDR5 RAM dəstləri.",
                IconName = "drive",
                DisplayOrder = 5,
                IsActive = true
            },
            new()
            {
                Slug = "accessories",
                NameEn = "Accessories",
                NameRu = "Аксессуары",
                NameAz = "Aksesuarlar",
                DescriptionEn = "Thunderbolt 4 docks, desktop hubs, and workstation connectivity.",
                DescriptionRu = "Док-станции Thunderbolt 4, хабы и аксессуары для рабочего стола.",
                DescriptionAz = "Thunderbolt 4 dok-stansiyaları, hablar və iş masası bağlantıları.",
                IconName = "dock",
                DisplayOrder = 6,
                IsActive = true
            }
        };

        db.Categories.AddRange(categories);
        await db.SaveChangesAsync();

        var catBySlug = categories.ToDictionary(c => c.Slug, c => c.Id);
        var now = DateTime.UtcNow;

        var products = BuildSeedProducts(catBySlug, now);
        db.Products.AddRange(products);
        await db.SaveChangesAsync();

        logger.LogInformation("Seeded {CategoryCount} categories and {ProductCount} hardware products.",
            categories.Count, products.Count);
    }

    private static List<Product> BuildSeedProducts(Dictionary<string, int> cat, DateTime now)
    {
        return new List<Product>
        {
            CreateProduct(
                sku: "TS-LAP-001", slug: "asus-rog-zephyrus-g16-oled", brand: "ASUS", categoryId: cat["laptops"],
                price: 4899m, discountPrice: 4499m, stock: 7, isFeatured: true, createdAt: now.AddDays(-20),
                nameEn: "ASUS ROG Zephyrus G16 OLED (2025)",
                nameRu: "ASUS ROG Zephyrus G16 OLED (2025)",
                nameAz: "ASUS ROG Zephyrus G16 OLED (2025)",
                shortEn: "16\" 240Hz ROG Nebula OLED, Intel Core Ultra 9, 32GB RAM, RTX 4090 in a CNC aluminum chassis.",
                shortRu: "16\" 240 Гц ROG Nebula OLED, Intel Core Ultra 9, 32 ГБ ОЗУ и RTX 4090 в алюминиевом корпусе.",
                shortAz: "16\" 240Hz ROG Nebula OLED, Intel Core Ultra 9, 32GB RAM və RTX 4090 CNC alüminium korpusda.",
                descEn: "Precision-milled from a solid block of aluminum, the ROG Zephyrus G16 pairs a 240Hz 2.5K OLED panel with flagship desktop-class graphics and tri-fan vapor chamber cooling for demanding 3D, AI, and gaming workloads.",
                descRu: "Фрезерованный из цельного алюминия ROG Zephyrus G16 объединяет 2.5K OLED-дисплей 240 Гц, флагманскую графику и испарительную камеру с тремя вентиляторами для 3D-рендеринга, ИИ и игр.",
                descAz: "Bütöv alüminiumdan hazırlanmış ROG Zephyrus G16 modeli 240Hz 2.5K OLED ekranı, flaqman qrafik performansı və üç pərli buxar kamerası soyutması ilə 3D, süni intellekt və oyunlar üçün idealdır.",
                specs: new[]
                {
                    Spec("Processor", "Процессор", "Prosessor", "Intel Core Ultra 9 185H (16 Cores, up to 5.1 GHz)", 1),
                    Spec("Graphics", "Видеокарта", "Videokart", "NVIDIA GeForce RTX 4090 Laptop 16GB GDDR6", 2),
                    Spec("Display", "Дисплей", "Ekran", "16\" 2560x1600 OLED, 240Hz, 0.2ms, 100% DCI-P3", 3),
                    Spec("Memory", "Оперативная память", "Operativ yaddaş", "32GB LPDDR5X-7467 (Dual Channel)", 4),
                    Spec("Storage", "Накопитель", "Yaddaş diski", "2TB PCIe 4.0 NVMe M.2 SSD", 5),
                    Spec("Weight", "Вес", "Çəki", "1.85 kg (CNC Aluminum)", 6)
                }),

            CreateProduct(
                sku: "TS-LAP-002", slug: "apple-macbook-pro-14-m4-pro", brand: "Apple", categoryId: cat["laptops"],
                price: 4699m, discountPrice: null, stock: 9, isFeatured: true, createdAt: now.AddDays(-19),
                nameEn: "Apple MacBook Pro 14\" M4 Pro",
                nameRu: "Apple MacBook Pro 14\" M4 Pro",
                nameAz: "Apple MacBook Pro 14\" M4 Pro",
                shortEn: "14-core M4 Pro silicon, 24GB unified memory, 1TB SSD, Liquid Retina XDR, and Thunderbolt 5.",
                shortRu: "14-ядерный чип M4 Pro, 24 ГБ объединённой памяти, 1 ТБ SSD, Liquid Retina XDR и Thunderbolt 5.",
                shortAz: "14 nüvəli M4 Pro prosessor, 24GB birləşmiş yaddaş, 1TB SSD, Liquid Retina XDR və Thunderbolt 5.",
                descEn: "Engineered for software engineers, video colorists, and studio producers, MacBook Pro 14 with M4 Pro delivers extreme single- and multi-core throughput, 1600-nit HDR display accuracy, and up to 22 hours of battery life.",
                descRu: "Созданный для разработчиков, колористов и саунд-продюсеров, MacBook Pro 14 с чипом M4 Pro обеспечивает высочайшую производительность, HDR-дисплей до 1600 нит и до 22 часов автономной работы.",
                descAz: "Proqram mühəndisləri və video montaj mütəxəssisləri üçün hazırlanmış M4 Pro çipli MacBook Pro 14 yüksək hesablama gücü, 1600 nit XDR ekran dəqiqliyi və 22 saata qədər batareya ömrü təqdim edir.",
                specs: new[]
                {
                    Spec("Chip", "Чип", "Çip", "Apple M4 Pro (14-Core CPU, 20-Core GPU)", 1),
                    Spec("Memory", "Память", "Yaddaş", "24GB Unified Memory (273 GB/s)", 2),
                    Spec("Storage", "Накопитель", "Yaddaş diski", "1TB Apple NVMe SSD", 3),
                    Spec("Display", "Дисплей", "Ekran", "14.2\" Liquid Retina XDR 3024x1964, ProMotion 120Hz", 4),
                    Spec("Ports", "Порты", "Portlar", "3x Thunderbolt 5, HDMI 2.1, SDXC, MagSafe 3", 5),
                    Spec("Battery", "Автономность", "Batareya", "Up to 22 hours video playback (72.4 Wh)", 6)
                }),

            CreateProduct(
                sku: "TS-LAP-003", slug: "lenovo-thinkpad-x1-carbon-g12", brand: "Lenovo", categoryId: cat["laptops"],
                price: 3899m, discountPrice: 3599m, stock: 4, isFeatured: true, createdAt: now.AddDays(-18),
                nameEn: "Lenovo ThinkPad X1 Carbon Gen 12",
                nameRu: "Lenovo ThinkPad X1 Carbon Gen 12",
                nameAz: "Lenovo ThinkPad X1 Carbon Gen 12",
                shortEn: "Featherweight 1.09 kg carbon-fiber enterprise ultrabook with 2.8K OLED and Intel vPro.",
                shortRu: "Ультралёгкий бизнес-ультрабук 1,09 кг из карбона с 2.8K OLED и платформой Intel vPro.",
                shortAz: "1.09 kq karbon lifli korporativ ultrabuk, 2.8K OLED ekran və Intel vPro təhlükəsizliyi.",
                descEn: "Built to MIL-STD-810H durability standards, ThinkPad X1 Carbon Gen 12 combines an iconic tactile keyboard, anti-glare 2.8K 120Hz OLED display, Wi-Fi 7 connectivity, and all-day mobile efficiency.",
                descRu: "Соответствующий стандарту MIL-STD-810H, ThinkPad X1 Carbon Gen 12 сочетает легендарную клавиатуру, антибликовый 2.8K OLED 120 Гц, Wi-Fi 7 и автономность на весь рабочий день.",
                descAz: "MIL-STD-810H dayanıqlıq standartına cavab verən ThinkPad X1 Carbon Gen 12 əfsanəvi klaviatura, parıltı əleyhinə 2.8K 120Hz OLED ekran və Wi-Fi 7 bağlantısını birləşdirir.",
                specs: new[]
                {
                    Spec("Processor", "Процессор", "Prosessor", "Intel Core Ultra 7 155U vPro", 1),
                    Spec("Display", "Дисплей", "Ekran", "14\" 2880x1800 OLED 120Hz Anti-Glare", 2),
                    Spec("Memory", "Оперативная память", "Operativ yaddaş", "32GB LPDDR5X-6400", 3),
                    Spec("Storage", "Накопитель", "Yaddaş diski", "1TB PCIe Gen4 Performance SSD", 4),
                    Spec("Security", "Безопасность", "Təhlükəsizlik", "dTPM 2.0, Fingerprint Reader, IR Camera Shutter", 5),
                    Spec("Weight", "Вес", "Çəki", "1.09 kg (Carbon Fiber + Magnesium)", 6)
                }),

            CreateProduct(
                sku: "TS-GPU-001", slug: "nvidia-rtx-5080-fe", brand: "NVIDIA", categoryId: cat["components"],
                price: 2799m, discountPrice: null, stock: 3, isFeatured: true, createdAt: now.AddDays(-17),
                nameEn: "NVIDIA GeForce RTX 5080 Founders Edition",
                nameRu: "NVIDIA GeForce RTX 5080 Founders Edition",
                nameAz: "NVIDIA GeForce RTX 5080 Founders Edition",
                shortEn: "Blackwell architecture GPU with 16GB GDDR7, PCIe 5.0, DLSS 4, and compact two-slot cooling.",
                shortRu: "Видеокарта на архитектуре Blackwell с 16 ГБ GDDR7, PCIe 5.0, DLSS 4 и двухслотовым охлаждением.",
                shortAz: "16GB GDDR7 yaddaşlı Blackwell arxitekturalı videokart, PCIe 5.0, DLSS 4 və 2-slot soyutma.",
                descEn: "Powered by NVIDIA Blackwell and fifth-generation Tensor Cores, the RTX 5080 Founders Edition brings breakthrough 4K ray tracing, AI inference acceleration, and a precision dual-flow-through acoustic cooler.",
                descRu: "На базе архитектуры NVIDIA Blackwell и тензорных ядер 5-го поколения, RTX 5080 Founders Edition открывает новый уровень трассировки лучей в 4K и ускорения ИИ в тихом двухслотовом корпусе.",
                descAz: "NVIDIA Blackwell arxitekturası və 5-ci nəsil Tensor nüvələri ilə RTX 5080 Founders Edition 4K ray-tracing və süni intellekt hesablamalarında rekord sürət təqdim edir.",
                specs: new[]
                {
                    Spec("Architecture", "Архитектура", "Arxitektura", "NVIDIA Blackwell (5th-Gen Tensor, 4th-Gen RT)", 1),
                    Spec("Video Memory", "Видеопамять", "Video yaddaş", "16GB GDDR7 (256-bit, 960 GB/s)", 2),
                    Spec("CUDA Cores", "Ядра CUDA", "CUDA nüvələri", "10,752", 3),
                    Spec("Interface", "Интерфейс", "İnterfeys", "PCI Express 5.0 x16", 4),
                    Spec("Outputs", "Видеовыходы", "Video çıxışlar", "3x DisplayPort 2.1b, 1x HDMI 2.1b", 5),
                    Spec("TGP", "Энергопотребление", "Enerji sərfiyyatı", "360W (16-pin 12V-2x6)", 6)
                }),

            CreateProduct(
                sku: "TS-CPU-001", slug: "amd-ryzen-9-9950x", brand: "AMD", categoryId: cat["components"],
                price: 1349m, discountPrice: 1249m, stock: 14, isFeatured: true, createdAt: now.AddDays(-16),
                nameEn: "AMD Ryzen 9 9950X 16-Core Processor",
                nameRu: "Процессор AMD Ryzen 9 9950X (16 ядер)",
                nameAz: "AMD Ryzen 9 9950X 16 nüvəli prosessor",
                shortEn: "16 cores, 32 threads, up to 5.7 GHz boost clock on Zen 5 architecture for Socket AM5.",
                shortRu: "16 ядер, 32 потока, разгон до 5,7 ГГц на архитектуре Zen 5 для сокета AM5.",
                shortAz: "AM5 soketi üçün Zen 5 arxitekturalı 16 nüvə, 32 axın və 5.7 GHz-ə qədər tezlik.",
                descEn: "Designed for uncompromising compilation, Blender rendering, and high-FPS multitasking, the Ryzen 9 9950X pairs 16 full Zen 5 cores with 80MB combined cache and full PCIe 5.0 / DDR5 support.",
                descRu: "Создан для тяжёлой компиляции, рендеринга и многопоточных задач: Ryzen 9 9950X оснащён 16 полноценными ядрами Zen 5, 80 МБ кэша и полной поддержкой PCIe 5.0 и DDR5.",
                descAz: "Ağır kompilyasiya, 3D render və çoxaxınlı işlər üçün yaradılmış Ryzen 9 9950X 16 Zen 5 nüvəsi, 80MB keş yaddaşı və PCIe 5.0 / DDR5 dəstəyi təqdim edir.",
                specs: new[]
                {
                    Spec("Cores / Threads", "Ядра / Потоки", "Nüvə / Axın", "16 Cores / 32 Threads", 1),
                    Spec("Clock Speed", "Тактовая частота", "Tezlik", "4.3 GHz Base / 5.7 GHz Max Boost", 2),
                    Spec("Socket", "Сокет", "Soket", "AM5 (4nm FinFET Zen 5)", 3),
                    Spec("Total Cache", "Общий кэш", "Ümumi keş", "80MB (16MB L2 + 64MB L3)", 4),
                    Spec("TDP", "Теплопакет", "TDP", "170W (Unlocked for Overclocking)", 5)
                }),

            CreateProduct(
                sku: "TS-MBD-001", slug: "asus-rog-strix-x870e-e", brand: "ASUS", categoryId: cat["components"],
                price: 989m, discountPrice: null, stock: 8, isFeatured: false, createdAt: now.AddDays(-15),
                nameEn: "ASUS ROG Strix X870E-E Gaming WiFi",
                nameRu: "Материнская плата ASUS ROG Strix X870E-E Gaming WiFi",
                nameAz: "ASUS ROG Strix X870E-E Gaming WiFi ana plata",
                shortEn: "Flagship AM5 ATX motherboard with 18+2+2 power stages, 5x M.2 slots, USB4, and Wi-Fi 7.",
                shortRu: "Флагманская плата AM5 ATX с питанием 18+2+2 фаз, 5 слотами M.2, USB4 и Wi-Fi 7.",
                shortAz: "18+2+2 faza qidalanma, 5 ədəd M.2 yuvası, USB4 və Wi-Fi 7 ilə flaqman AM5 ATX ana plata.",
                descEn: "Built for Ryzen 9000 series builds, the ROG Strix X870E-E delivers massive VRM thermal headroom, tool-less M.2 Q-Release latches, dual 40Gbps USB4 ports, and 5Gb Ethernet.",
                descRu: "Спроектирована для процессоров Ryzen 9000: мощная подсистема питания с массивными радиаторами, безвинтовые защёлки M.2 Q-Release, два порта USB4 40 Гбит/с и сеть 5 Гбит/с.",
                descAz: "Ryzen 9000 sistemləri üçün hazırlanmış ROG Strix X870E-E güclü VRM soyutması, alətsiz M.2 Q-Release kilidləri, iki 40Gbps USB4 portu və 5Gb Ethernet təklif edir.",
                specs: new[]
                {
                    Spec("Chipset & Socket", "Чипсет и сокет", "Çipset və soket", "AMD X870E / Socket AM5 (ATX)", 1),
                    Spec("Power Design", "Подсистема питания", "Qidalanma sistemi", "18+2+2 Teamed Stages (110A)", 2),
                    Spec("Memory Support", "Поддержка ОЗУ", "RAM dəstəyi", "4x DDR5 DIMM, up to 256GB (8400+ MT/s OC)", 3),
                    Spec("Storage", "Слоты накопителей", "Disk yuvaları", "3x PCIe 5.0 M.2 + 2x PCIe 4.0 M.2", 4),
                    Spec("Networking", "Сеть", "Şəbəkə", "Wi-Fi 7 (320 MHz) + Realtek 5Gb Ethernet", 5)
                }),

            CreateProduct(
                sku: "TS-PSU-001", slug: "seasonic-vertex-gx-1000", brand: "Seasonic", categoryId: cat["components"],
                price: 469m, discountPrice: null, stock: 11, isFeatured: false, createdAt: now.AddDays(-14),
                nameEn: "Seasonic Vertex GX-1000 80+ Gold ATX 3.1",
                nameRu: "Блок питания Seasonic Vertex GX-1000 80+ Gold ATX 3.1",
                nameAz: "Seasonic Vertex GX-1000 80+ Gold ATX 3.1 qida bloku",
                shortEn: "1000W fully modular ATX 3.1 / PCIe 5.1 PSU with native 12V-2x6 cable and 12-year warranty.",
                shortRu: "Модульный блок питания 1000 Вт стандарта ATX 3.1 / PCIe 5.1 с кабелем 12V-2x6 и гарантией 12 лет.",
                shortAz: "1000W tam modulyar ATX 3.1 / PCIe 5.1 qida bloku, yerli 12V-2x6 kabeli və 12 illik zəmanət.",
                descEn: "Engineered with Japanese 105°C capacitors and hybrid silent fan control, the Vertex GX-1000 cleanly handles 200% GPU power excursions for flagship RTX 50-series workstations.",
                descRu: "Построен на японских конденсаторах 105°C с гибридным бесшумным режимом вентилятора. Vertex GX-1000 уверенно выдерживает пиковые нагрузки современных флагманских видеокарт.",
                descAz: "Yaponiya istehsalı 105°C kondensatorlar və hibrid səssiz soyutma ilə Vertex GX-1000 flaqman RTX 50 seriyalı sistemlərin yüksək enerji tələblərini sabit qarşılayır.",
                specs: new[]
                {
                    Spec("Output Power", "Мощность", "Güc", "1000W Continuous", 1),
                    Spec("Efficiency", "Сертификат КПД", "Səmərəlilik", "80 PLUS Gold (up to 90%)", 2),
                    Spec("Standard", "Стандарт", "Standart", "ATX 3.1 & PCIe 5.1 (Native 12V-2x6)", 3),
                    Spec("Cabling", "Кабели", "Kabellər", "100% Fully Modular Braided Pattern", 4),
                    Spec("Cooling", "Охлаждение", "Soyutma", "135mm Fluid Dynamic Bearing Fan (Zero RPM)", 5)
                }),

            CreateProduct(
                sku: "TS-MON-001", slug: "lg-ultragear-32gs95ue-oled", brand: "LG", categoryId: cat["monitors"],
                price: 2599m, discountPrice: 2349m, stock: 5, isFeatured: true, createdAt: now.AddDays(-13),
                nameEn: "LG UltraGear 32GS95UE 32\" 4K OLED Dual-Mode",
                nameRu: "Монитор LG UltraGear 32GS95UE 32\" 4K OLED 240 Гц",
                nameAz: "LG UltraGear 32GS95UE 32\" 4K OLED Dual-Mode monitor",
                shortEn: "32\" MLA+ WOLED monitor switching seamlessly between 4K 240Hz and FHD 480Hz at 0.03ms GTG.",
                shortRu: "32\" MLA+ WOLED монитор с переключением между 4K 240 Гц и FHD 480 Гц и откликом 0,03 мс.",
                shortAz: "0.03ms cavab müddəti ilə 4K 240Hz və FHD 480Hz arasında keçid edən 32\" MLA+ WOLED monitor.",
                descEn: "Featuring VESA Dual-Mode certification, an anti-glare low-reflection coating, and integrated Pixel Sound speakers, the 32GS95UE delivers infinite OLED contrast for both cinematic 4K creation and 480Hz competitive esports.",
                descRu: "Сертификация VESA Dual-Mode, антибликовое покрытие и встроенная акустика Pixel Sound обеспечивают бесконечную контрастность OLED как для 4K-работы, так и для киберспорта на 480 Гц.",
                descAz: "VESA Dual-Mode sertifikatı, parıltı əleyhinə örtük və Pixel Sound daxili səs sistemi ilə 32GS95UE həm 4K yaradıcılıq, həm də 480Hz kiberidman üçün sonsuz kontrast verir.",
                specs: new[]
                {
                    Spec("Panel", "Матрица", "Panel", "31.5\" MLA+ WOLED Anti-Glare", 1),
                    Spec("Resolution & Refresh", "Разрешение и частота", "Təsvir və tezlik", "3840x2160 @ 240Hz / 1920x1080 @ 480Hz", 2),
                    Spec("Response Time", "Время отклика", "Cavab müddəti", "0.03ms (GtG)", 3),
                    Spec("Color Gamut", "Цветовой охват", "Rəng əhatəsi", "98.5% DCI-P3, DisplayHDR True Black 400", 4),
                    Spec("Connectivity", "Разъёмы", "Bağlantılar", "2x HDMI 2.1, 1x DisplayPort 1.4, USB Hub", 5)
                }),

            CreateProduct(
                sku: "TS-MON-002", slug: "dell-ultrasharp-u2724de", brand: "Dell", categoryId: cat["monitors"],
                price: 1199m, discountPrice: null, stock: 12, isFeatured: false, createdAt: now.AddDays(-12),
                nameEn: "Dell UltraSharp U2724DE 27\" QHD 120Hz Hub",
                nameRu: "Монитор Dell UltraSharp U2724DE 27\" QHD 120 Гц Hub",
                nameAz: "Dell UltraSharp U2724DE 27\" QHD 120Hz Hub monitor",
                shortEn: "27\" IPS Black 2000:1 display with 120Hz ComfortView Plus, Thunderbolt 4 (90W), and 2.5GbE.",
                shortRu: "27\" IPS Black (2000:1) с частотой 120 Гц, хабом Thunderbolt 4 (90 Вт) и портом 2.5GbE.",
                shortAz: "120Hz IPS Black (2000:1) ekran, 90W Thunderbolt 4 habı və daxili 2.5GbE şəbəkə portu.",
                descEn: "Designed as a single-cable productivity hub, the UltraSharp U2724DE pairs deep 2000:1 IPS Black contrast and an ambient light sensor with built-in KVM switching and 90W Thunderbolt 4 power delivery.",
                descRu: "Создан как центр рабочего места с подключением одним кабелем: глубокий чёрный IPS Black 2000:1, датчик освещённости, встроенный KVM-переключатель и питание 90 Вт по Thunderbolt 4.",
                descAz: "Tək kabelli iş masası mərkəzi kimi hazırlanmış UltraSharp U2724DE dərin 2000:1 IPS Black kontrastı, işıq sensoru, daxili KVM və 90W Thunderbolt 4 şarjı təmin edir.",
                specs: new[]
                {
                    Spec("Panel", "Матрица", "Panel", "27\" IPS Black (2000:1 Static Contrast)", 1),
                    Spec("Resolution & Refresh", "Разрешение и частота", "Təsvir və tezlik", "2560x1440 (QHD) @ 120Hz", 2),
                    Spec("Color Accuracy", "Точность цвета", "Rəng dəqiqliyi", "98% DCI-P3, 100% sRGB (Delta E < 2)", 3),
                    Spec("Docking Hub", "Встроенный хаб", "Daxili hab", "Thunderbolt 4 (90W PD), 2.5GbE RJ45, KVM", 4),
                    Spec("Ergonomics", "Эргономика", "Erqonomika", "Height, Pivot, Swivel, Tilt + VESA 100x100", 5)
                }),

            CreateProduct(
                sku: "TS-MON-003", slug: "asus-proart-pa329crv", brand: "ASUS", categoryId: cat["monitors"],
                price: 1649m, discountPrice: null, stock: 6, isFeatured: false, createdAt: now.AddDays(-11),
                nameEn: "ASUS ProArt Display PA329CRV 32\" 4K HDR",
                nameRu: "Монитор ASUS ProArt PA329CRV 32\" 4K HDR",
                nameAz: "ASUS ProArt PA329CRV 32\" 4K HDR monitor",
                shortEn: "Calman Verified 31.5\" 4K UHD creator monitor with 98% DCI-P3 and 96W USB-C power delivery.",
                shortRu: "31,5\" 4K UHD монитор с сертификацией Calman Verified, охватом 98% DCI-P3 и USB-C 96 Вт.",
                shortAz: "Calman Verified sertifikatlı 31.5\" 4K UHD yaradıcılıq monitoru, 98% DCI-P3 və 96W USB-C.",
                descEn: "Factory pre-calibrated to Delta E < 2 for photo retouching, UI design, and broadcast grading, the ProArt PA329CRV offers a sleek 30%-slimmer profile and single-cable 96W USB-C daisy-chaining.",
                descRu: "Заводская калибровка Delta E < 2 для ретуши, дизайна и цветокоррекции. ProArt PA329CRV отличается тонким корпусом и подключением по USB-C 96 Вт с поддержкой Daisy Chain.",
                descAz: "Foto, UI dizayn və rəng korreksiyası üçün fabrikdə Delta E < 2 dəqiqliyi ilə kalibrlənmiş ProArt PA329CRV incə gövdə və 96W USB-C bağlantısı təqdim edir.",
                specs: new[]
                {
                    Spec("Panel", "Матрица", "Panel", "31.5\" 4K UHD (3840x2160) IPS", 1),
                    Spec("Calibration", "Калибровка", "Kalibrləmə", "Calman Verified, Factory Delta E < 2", 2),
                    Spec("Color Space", "Цветовой охват", "Rəng məkanı", "98% DCI-P3, 100% sRGB, 10-bit", 3),
                    Spec("USB-C Hub", "Порт USB-C", "USB-C portu", "96W Power Delivery + DisplayPort Daisy-Chain", 4),
                    Spec("HDR", "Поддержка HDR", "HDR dəstəyi", "VESA DisplayHDR 400", 5)
                }),

            CreateProduct(
                sku: "TS-KBD-001", slug: "keychron-q1-max-wireless", brand: "Keychron", categoryId: cat["peripherals"],
                price: 429m, discountPrice: null, stock: 15, isFeatured: true, createdAt: now.AddDays(-10),
                nameEn: "Keychron Q1 Max Aluminum Wireless Mechanical",
                nameRu: "Клавиатура Keychron Q1 Max Wireless (алюминий)",
                nameAz: "Keychron Q1 Max alüminium simsiz mexaniki klaviatura",
                shortEn: "75% CNC 6063 aluminum custom mechanical keyboard with 2.4GHz 1000Hz, Bluetooth 5.1, and QMK/VIA.",
                shortRu: "Механическая клавиатура 75% из ЧПУ-алюминия 6063 с 2,4 ГГц 1000 Гц, Bluetooth 5.1 и QMK/VIA.",
                shortAz: "CNC 6063 alüminium gövdəli 75% mexaniki klaviatura: 2.4GHz 1000Hz, Bluetooth 5.1 və QMK/VIA.",
                descEn: "Engineered with a double-gasket mount, multi-layer acoustic foam, hot-swappable Gateron Jupiter Banana tactile switches, and a programmable rotary knob for macOS, Windows, and Linux.",
                descRu: "Двойная конструкция Gasket Mount, многослойная шумоизоляция, горячая замена переключателей Gateron Jupiter Banana и программируемый энкодер для macOS, Windows и Linux.",
                descAz: "İkiqat gasket konstruksiyası, çoxqatlı akustik izolyasiya, hot-swap Gateron Jupiter Banana düymələri və proqramlaşdırılan fırlanan idarəetmə düyməsi ilə macOS, Windows və Linux dəstəyi.",
                specs: new[]
                {
                    Spec("Layout & Case", "Формат и корпус", "Format və korpus", "75% CNC Machined 6063 Aluminum (1.72 kg)", 1),
                    Spec("Connectivity", "Подключение", "Bağlantı", "2.4GHz (1000Hz), Bluetooth 5.1, USB-C Wired", 2),
                    Spec("Switches", "Переключатели", "Düymələr", "Hot-Swappable Gateron Jupiter Banana (Tactile)", 3),
                    Spec("Keycaps", "Кейкапы", "Düymə başlıqları", "Double-Shot KSA Profile PBT", 4),
                    Spec("Firmware", "Прошивка", "Proqram təminatı", "Open-Source QMK & VIA Web Configurator", 5)
                }),

            CreateProduct(
                sku: "TS-MSE-001", slug: "logitech-g-pro-x-superlight-2", brand: "Logitech", categoryId: cat["peripherals"],
                price: 319m, discountPrice: 289m, stock: 18, isFeatured: false, createdAt: now.AddDays(-9),
                nameEn: "Logitech G Pro X Superlight 2 Wireless",
                nameRu: "Мышь Logitech G Pro X Superlight 2 Wireless",
                nameAz: "Logitech G Pro X Superlight 2 simsiz siçan",
                shortEn: "60g tournament-grade wireless mouse with HERO 2 44K sensor, 8KHz polling, and LIGHTFORCE switches.",
                shortRu: "Киберспортивная беспроводная мышь весом 60 г с сенсором HERO 2 44K, 8 кГц и LIGHTFORCE.",
                shortAz: "60 qramlıq kiberidman simsiz siçanı: HERO 2 44K sensoru, 8KHz tezlik və LIGHTFORCE düymələri.",
                descEn: "Co-developed with top esports pros, the Pro X Superlight 2 achieves sub-millisecond wireless precision with optical-mechanical hybrid switches, zero-additive PTFE feet, and 95-hour USB-C battery endurance.",
                descRu: "Разработана совместно с профессиональными игроками: оптико-механические переключатели LIGHTFORCE, тефлоновые глайды без примесей и до 95 часов работы от одного заряда USB-C.",
                descAz: "Peşəkar oyunçularla birlikdə hazırlanmış Pro X Superlight 2 hibrid optik-mexaniki düymələr, təmiz PTFE sürüşmə ayaqları və 95 saata qədər USB-C batareya ömrü verir.",
                specs: new[]
                {
                    Spec("Sensor", "Сенсор", "Sensor", "HERO 2 Optical (100–44,000 DPI, 888 IPS)", 1),
                    Spec("Polling Rate", "Частота опроса", "Sorğu tezliyi", "Up to 8000 Hz LIGHTSPEED Wireless", 2),
                    Spec("Switches", "Переключатели", "Düymələr", "LIGHTFORCE Hybrid Optical-Mechanical", 3),
                    Spec("Weight", "Вес", "Çəki", "60 g", 4),
                    Spec("Battery Life", "Автономность", "Batareya ömrü", "Up to 95 hours (USB-C Fast Charge)", 5)
                }),

            CreateProduct(
                sku: "TS-AUD-001", slug: "sony-wh-1000xm5-midnight", brand: "Sony", categoryId: cat["peripherals"],
                price: 649m, discountPrice: null, stock: 10, isFeatured: false, createdAt: now.AddDays(-8),
                nameEn: "Sony WH-1000XM5 Wireless ANC Headphones",
                nameRu: "Наушники Sony WH-1000XM5 с шумоподавлением",
                nameAz: "Sony WH-1000XM5 simsiz ANC qulaqlıq",
                shortEn: "Reference-class active noise cancellation with dual processors, 8 mics, LDAC Hi-Res, and 30h battery.",
                shortRu: "Эталонное активное шумоподавление с 2 процессорами, 8 микрофонами, LDAC и 30 ч работы.",
                shortAz: "İki prosessor, 8 mikrofon, LDAC Hi-Res dəstəyi və 30 saatlıq batareya ilə premium ANC qulaqlıq.",
                descEn: "Engineered for deep-focus coding sessions and flights, the WH-1000XM5 features custom 30mm carbon-fiber dome drivers, seamless dual-device Multipoint Bluetooth, and crystal-clear AI voice isolation.",
                descRu: "Идеальны для глубокой концентрации и перелётов: 30-мм драйверы из углеволокна, одновременное подключение к двум устройствам Multipoint и ИИ-шумоподавление голоса при звонках.",
                descAz: "Fokuslanmış iş və səyahətlər üçün 30mm karbon lifli dinamiklər, eyni anda iki cihaza Multipoint bağlantısı və zənglər üçün süni intellekt səs təmizləməsi təqdim edir.",
                specs: new[]
                {
                    Spec("Noise Cancelling", "Шумоподавление", "Səs-küy ləğvi", "Integrated V1 + HD QN1 Dual Processors, 8 Mics", 1),
                    Spec("Driver", "Динамик", "Dinamik", "30mm Precision Carbon Fiber Composite", 2),
                    Spec("Audio Codecs", "Кодеки", "Audio kodeklər", "LDAC, AAC, SBC (Hi-Res Audio Wireless)", 3),
                    Spec("Battery Life", "Автономность", "Batareya", "30 hours (ANC On) / 3 min charge = 3h playback", 4),
                    Spec("Weight", "Вес", "Çəki", "250 g (Soft-Fit Leatherette)", 5)
                }),

            CreateProduct(
                sku: "TS-SSD-001", slug: "samsung-990-pro-2tb-nvme", brand: "Samsung", categoryId: cat["storage-memory"],
                price: 369m, discountPrice: 329m, stock: 22, isFeatured: true, createdAt: now.AddDays(-7),
                nameEn: "Samsung 990 PRO 2TB PCIe 4.0 NVMe M.2",
                nameRu: "Накопитель Samsung 990 PRO 2 ТБ PCIe 4.0 NVMe M.2",
                nameAz: "Samsung 990 PRO 2TB PCIe 4.0 NVMe M.2 SSD",
                shortEn: "7,450 MB/s sequential read, 1,400K IOPS random performance, and nickel-coated thermal control.",
                shortRu: "Скорость чтения до 7450 МБ/с, 1400K IOPS и никелированный контроллер с термоконтролем.",
                shortAz: "7450 MB/s oxuma sürəti, 1400K IOPS və nikel örtüklü termal nəzarətli flaqman NVMe SSD.",
                descEn: "Maximizing the PCIe 4.0 interface for heavy 4K video timelines, large codebases, and instant level streaming, the 990 PRO 2TB delivers 50% better power efficiency per watt with a 1,200 TBW endurance rating.",
                descRu: "Раскрывает максимум интерфейса PCIe 4.0 для 4K-видеомонтажа, больших проектов и мгновенной загрузки игр: на 50% выше энергоэффективность и ресурс 1200 TBW.",
                descAz: "4K video montaj, böyük layihələr və ani oyun yüklənməsi üçün PCIe 4.0 imkanlarını maksimuma çatdıran 990 PRO 2TB yüksək enerji səmərəliliyi və 1200 TBW resurs təklif edir.",
                specs: new[]
                {
                    Spec("Capacity", "Объём", "Həcm", "2 TB (M.2 2280)", 1),
                    Spec("Interface", "Интерфейс", "İnterfeys", "PCIe Gen 4.0 x4, NVMe 2.0", 2),
                    Spec("Sequential R/W", "Чтение / Запись", "Oxuma / Yazma", "Up to 7,450 MB/s Read / 6,900 MB/s Write", 3),
                    Spec("Cache", "Буфер DRAM", "DRAM keş", "2GB Low Power DDR4 SDRAM", 4),
                    Spec("Endurance", "Ресурс", "Resurs", "1,200 TBW (5-Year Limited Warranty)", 5)
                }),

            CreateProduct(
                sku: "TS-RAM-001", slug: "gskill-trident-z5-neo-32gb", brand: "G.SKILL", categoryId: cat["storage-memory"],
                price: 279m, discountPrice: null, stock: 16, isFeatured: false, createdAt: now.AddDays(-6),
                nameEn: "G.SKILL Trident Z5 Neo RGB 32GB DDR5-6000",
                nameRu: "Память G.SKILL Trident Z5 Neo RGB 32 ГБ DDR5-6000",
                nameAz: "G.SKILL Trident Z5 Neo RGB 32GB DDR5-6000 RAM",
                shortEn: "2x16GB matched DDR5-6000 CL30 kit tuned for AMD EXPO and low-latency AM5 performance.",
                shortRu: "Комплект 2×16 ГБ DDR5-6000 CL30 с профилями AMD EXPO и низкими таймингами для AM5.",
                shortAz: "AMD EXPO dəstəkli, AM5 platforması üçün aşağı gecikməli 2x16GB DDR5-6000 CL30 yaddaş dəsti.",
                descEn: "Hand-screened DDR5 ICs housed in a dual-textured matte black brushed-aluminum heatspreader deliver the sweet-spot 6000 MT/s CL30 latency required by Ryzen 9000 processors.",
                descRu: "Отборные чипы DDR5 в матовом чёрном алюминиевом радиаторе обеспечивают оптимальную частоту 6000 МТ/с и тайминги CL30 для процессоров Ryzen 9000.",
                descAz: "Seçilmiş DDR5 çipləri və mat qara alüminium soyuducu ilə Ryzen 9000 prosessorları üçün ideal 6000 MT/s CL30 sabitliyini təmin edir.",
                specs: new[]
                {
                    Spec("Capacity", "Объём", "Həcm", "32GB (2 x 16GB Dual Channel Kit)", 1),
                    Spec("Speed", "Частота", "Tezlik", "DDR5-6000 MT/s (PC5-48000)", 2),
                    Spec("Timings", "Тайминги", "Gecikmələr", "CL30-38-38-96 @ 1.35V", 3),
                    Spec("Profile", "Профили разгона", "Profil", "AMD EXPO Certified", 4),
                    Spec("Heatspreader", "Охлаждение", "Soyuducu", "Brushed Aluminum + Streamlined RGB Lightbar", 5)
                }),

            CreateProduct(
                sku: "TS-DOC-001", slug: "caldigit-ts4-thunderbolt-4", brand: "CalDigit", categoryId: cat["accessories"],
                price: 749m, discountPrice: null, stock: 2, isFeatured: false, createdAt: now.AddDays(-5),
                nameEn: "CalDigit TS4 18-Port Thunderbolt 4 Dock",
                nameRu: "Док-станция CalDigit TS4 (18 портов, Thunderbolt 4)",
                nameAz: "CalDigit TS4 18 portlu Thunderbolt 4 dok-stansiyası",
                shortEn: "18 workstation ports, 98W host charging, 2.5GbE Ethernet, DisplayPort 1.4, and UHS-II SD/microSD.",
                shortRu: "18 портов, зарядка 98 Вт, Ethernet 2,5 Гбит/с, DisplayPort 1.4 и картридеры UHS-II SD/microSD.",
                shortAz: "18 port, 98W enerji ötürülməsi, 2.5GbE şəbəkə, DisplayPort 1.4 və UHS-II SD/microSD oxuyucuları.",
                descEn: "The gold-standard single-cable workstation dock for MacBook Pro and Windows Thunderbolt laptops. Machined aluminum ribs provide silent fanless cooling while driving dual 6K displays and high-speed RAID storage.",
                descRu: "Эталонная док-станция с подключением одним кабелем для MacBook Pro и ноутбуков Windows. Ребристый алюминиевый корпус обеспечивает бесшумное пассивное охлаждение при работе с двумя 6K-дисплеями.",
                descAz: "MacBook Pro və Windows noutbukları üçün tək kabelli etalon dok-stansiya. Alüminium korpus iki 6K monitor və sürətli yaddaş disklərini səssiz soyutma ilə dəstəkləyir.",
                specs: new[]
                {
                    Spec("Total Ports", "Количество портов", "Port sayı", "18 Ports (3x TB4, 8x USB 10Gbps, DP 1.4, 2.5GbE)", 1),
                    Spec("Host Charging", "Питание ноутбука", "Şarj gücü", "98W Power Delivery", 2),
                    Spec("Display Support", "Поддержка дисплеев", "Ekran dəstəyi", "Up to Dual 6K 60Hz or Single 8K 60Hz", 3),
                    Spec("Card Readers", "Картридеры", "Kart oxuyucular", "Front UHS-II SD 4.0 & microSD 4.0 (312 MB/s)", 4),
                    Spec("Ethernet", "Сеть", "Şəbəkə", "2.5 Gigabit Ethernet (2.5GbE)", 5)
                })
        };
    }

    private static Product CreateProduct(
        string sku, string slug, string brand, int categoryId,
        decimal price, decimal? discountPrice, int stock, bool isFeatured, DateTime createdAt,
        string nameEn, string nameRu, string nameAz,
        string shortEn, string shortRu, string shortAz,
        string descEn, string descRu, string descAz,
        ProductSpecification[] specs)
    {
        return new Product
        {
            Sku = sku,
            Slug = slug,
            Brand = brand,
            CategoryId = categoryId,
            Price = price,
            DiscountPrice = discountPrice,
            StockQuantity = stock,
            IsFeatured = isFeatured,
            IsActive = true,
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt,
            NameEn = nameEn,
            NameRu = nameRu,
            NameAz = nameAz,
            ShortDescriptionEn = shortEn,
            ShortDescriptionRu = shortRu,
            ShortDescriptionAz = shortAz,
            DescriptionEn = descEn,
            DescriptionRu = descRu,
            DescriptionAz = descAz,
            Images = new List<ProductImage>
            {
                new()
                {
                    Url = $"/images/products/{slug}-1.svg",
                    AltText = nameEn,
                    IsPrimary = true,
                    DisplayOrder = 1
                },
                new()
                {
                    Url = $"/images/products/{slug}-2.svg",
                    AltText = $"{nameEn} technical specification sheet",
                    IsPrimary = false,
                    DisplayOrder = 2
                }
            },
            Specifications = specs.ToList()
        };
    }

    private static ProductSpecification Spec(string en, string ru, string az, string value, int order) =>
        new()
        {
            KeyEn = en,
            KeyRu = ru,
            KeyAz = az,
            Value = value,
            DisplayOrder = order
        };
}
