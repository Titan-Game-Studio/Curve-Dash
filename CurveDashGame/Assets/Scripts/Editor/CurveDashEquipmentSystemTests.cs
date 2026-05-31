using NUnit.Framework;
using UnityEngine;

namespace STG.CurveDash.Tests
{
    public class MockInventoryScanner : IInventoryScanner
    {
        public OffHandData Arrow { get; set; }
        public OffHandData Shield { get; set; }
        public OneHandedWeaponData OneHandedWeapon { get; set; }
        public BowData Bow { get; set; }

        public OffHandData FindFirstArrow() => Arrow;
        public OffHandData FindFirstShield() => Shield;
        public OneHandedWeaponData FindFirstOneHandedWeapon() => OneHandedWeapon;
        public BowData FindFirstBow() => Bow;
    }

    [TestFixture]
    public class CurveDashEquipmentSystemTests
    {
        private IEquipmentResolver _resolver;
        private MockInventoryScanner _scanner;
        private SmartEquipService _service;

        [SetUp]
        public void Setup()
        {
            _resolver = new EquipmentResolver();
            _scanner = new MockInventoryScanner();
            _service = new SmartEquipService(_resolver, _scanner);
        }

        private T CreateItem<T>(string name) where T : EquippableData
        {
            var item = ScriptableObject.CreateInstance<T>();
            item.name = name;
            return item;
        }

        [Test]
        public void TwoHandedWeapon_GoesToLeftHand_ClearsRightHand()
        {
            var twoH = CreateItem<TwoHandedWeaponData>("GreatSword");
            var assignment = _service.SmartEquip(null, null, twoH);

            Assert.IsTrue(assignment.IsValid);
            Assert.AreEqual(twoH, assignment.LeftHand);
            Assert.IsNull(assignment.RightHand);
        }

        [Test]
        public void Bow_Equipped_DoesNotAutoEquipArrow()
        {
            var bow = CreateItem<BowData>("Reflex Bow");
            var arrow = CreateItem<OffHandData>("Wooden Arrow");
            arrow.SubType = OffHandType.Arrow;

            _scanner.Arrow = arrow;

            var assignment = _service.SmartEquip(null, null, bow);

            Assert.IsTrue(assignment.IsValid);
            Assert.AreEqual(bow, assignment.LeftHand);
            Assert.IsNull(assignment.RightHand);
        }

        [Test]
        public void Bow_Equipped_KeepsRightHandNull()
        {
            var bow = CreateItem<BowData>("Reflex Bow");
            _scanner.Arrow = null;

            var assignment = _service.SmartEquip(null, null, bow);

            Assert.IsTrue(assignment.IsValid);
            Assert.AreEqual(bow, assignment.LeftHand);
            Assert.IsNull(assignment.RightHand);
        }

        [Test]
        public void Arrow_Equipped_WithBowEquipped_ReturnsValid()
        {
            var arrow = CreateItem<OffHandData>("Wooden Arrow");
            arrow.SubType = OffHandType.Arrow;

            var bow = CreateItem<BowData>("Reflex Bow");

            var assignment = _service.SmartEquip(bow, null, arrow);

            Assert.IsTrue(assignment.IsValid);
            Assert.AreEqual(bow, assignment.LeftHand);
            Assert.AreEqual(arrow, assignment.RightHand);
        }

        [Test]
        public void Arrow_Equipped_NoBowEquipped_ReturnsInvalid()
        {
            var arrow = CreateItem<OffHandData>("Wooden Arrow");
            arrow.SubType = OffHandType.Arrow;

            var assignment = _service.SmartEquip(null, null, arrow);

            Assert.IsFalse(assignment.IsValid);
        }

        [Test]
        public void OneHandedSword_Equipped_DoesNotAutoEquipShield()
        {
            var sword = CreateItem<OneHandedWeaponData>("Rusty Sword");
            var shield = CreateItem<OffHandData>("Wooden Shield");
            shield.SubType = OffHandType.Shield;

            _scanner.Shield = shield;

            var assignment = _service.SmartEquip(null, null, sword);

            Assert.IsTrue(assignment.IsValid);
            Assert.IsNull(assignment.LeftHand);
            Assert.AreEqual(sword, assignment.RightHand);
        }

        [Test]
        public void Shield_Equipped_DoesNotAutoEquipSword()
        {
            var shield = CreateItem<OffHandData>("Wooden Shield");
            shield.SubType = OffHandType.Shield;

            var sword = CreateItem<OneHandedWeaponData>("Rusty Sword");
            _scanner.OneHandedWeapon = sword;

            var assignment = _service.SmartEquip(null, null, shield);

            Assert.IsTrue(assignment.IsValid);
            Assert.AreEqual(shield, assignment.LeftHand);
            Assert.IsNull(assignment.RightHand);
        }

        // ---- Displaced item scenarios (items that must return to inventory) ----

        [Test]
        public void Shield_EquippedWhenBowAndArrow_ClearsArrow()
        {
            var bow    = CreateItem<BowData>("Reflex Bow");
            var arrow  = CreateItem<OffHandData>("Wooden Arrow"); arrow.SubType = OffHandType.Arrow;
            var shield = CreateItem<OffHandData>("Wooden Shield"); shield.SubType = OffHandType.Shield;

            // State: Bow (left) + Arrow (right)
            var assignment = _service.SmartEquip(bow, arrow, shield);

            Assert.IsTrue(assignment.IsValid);
            Assert.AreEqual(shield, assignment.LeftHand, "Shield should be in left hand");
            Assert.IsNull(assignment.RightHand, "Arrow must be cleared when Bow is displaced");
        }

        [Test]
        public void TwoHanded_EquippedWhenBowAndArrow_ClearsBothHands()
        {
            var bow   = CreateItem<BowData>("Reflex Bow");
            var arrow = CreateItem<OffHandData>("Wooden Arrow"); arrow.SubType = OffHandType.Arrow;
            var twoH  = CreateItem<TwoHandedWeaponData>("Great Axe");

            var assignment = _service.SmartEquip(bow, arrow, twoH);

            Assert.IsTrue(assignment.IsValid);
            Assert.AreEqual(twoH, assignment.LeftHand);
            Assert.IsNull(assignment.RightHand, "Arrow must be cleared when replaced by 2H");
        }

        [Test]
        public void OneHanded_EquippedWhenTwoHandedEquipped_ClearsTwoHanded()
        {
            var twoH  = CreateItem<TwoHandedWeaponData>("Great Hammer");
            var sword = CreateItem<OneHandedWeaponData>("Cutlass");

            // State: TwoHanded (left), right=null
            var assignment = _service.SmartEquip(twoH, null, sword);

            Assert.IsTrue(assignment.IsValid);
            Assert.IsNull(assignment.LeftHand, "2H must be cleared when switching to 1H");
            Assert.AreEqual(sword, assignment.RightHand);
        }

        [Test]
        public void Unequip_Bow_ClearsArrowFromRightHand()
        {
            var bow   = CreateItem<BowData>("Reflex Bow");
            var arrow = CreateItem<OffHandData>("Wooden Arrow"); arrow.SubType = OffHandType.Arrow;

            // State: Bow (left) + Arrow (right), remove Bow
            var assignment = _service.SmartUnequip(bow, arrow, bow);

            Assert.IsTrue(assignment.IsValid);
            Assert.IsNull(assignment.LeftHand);
            Assert.IsNull(assignment.RightHand, "Arrow must be cleared when Bow is removed");
        }

        [Test]
        public void Sword_EquippedWhenBowAndArrow_ClearsBothBowAndArrow()
        {
            var bow   = CreateItem<BowData>("Reflex Bow");
            var arrow = CreateItem<OffHandData>("Wooden Arrow"); arrow.SubType = OffHandType.Arrow;
            var sword = CreateItem<OneHandedWeaponData>("Iron Sword");

            // Bow is NOT compatible with Sword — both Bow and Arrow must be displaced
            var assignment = _service.SmartEquip(bow, arrow, sword);

            Assert.IsTrue(assignment.IsValid);
            Assert.IsNull(assignment.LeftHand, "Bow must be cleared when equipping Sword");
            Assert.AreEqual(sword, assignment.RightHand);
        }

        [Test]
        public void Bow_EquippedWhenSwordAndShield_ClearsBoth()
        {
            var shield = CreateItem<OffHandData>("Wooden Shield"); shield.SubType = OffHandType.Shield;
            var sword  = CreateItem<OneHandedWeaponData>("Iron Sword");
            var bow    = CreateItem<BowData>("Reflex Bow");

            // State: left=Shield, right=Sword → equip Bow
            var assignment = _service.SmartEquip(shield, sword, bow);

            Assert.IsTrue(assignment.IsValid);
            Assert.AreEqual(bow, assignment.LeftHand);
            Assert.IsNull(assignment.RightHand, "Sword must be cleared when equipping Bow");
        }

        [Test]
        public void Bow_EquippedWhenDualWield_ClearsBothSwords()
        {
            var sword1 = CreateItem<OneHandedWeaponData>("Iron Sword");
            var sword2 = CreateItem<OneHandedWeaponData>("Steel Sword");
            var bow    = CreateItem<BowData>("Reflex Bow");

            // State: left=Sword1 (dual), right=Sword2 → equip Bow
            var assignment = _service.SmartEquip(sword1, sword2, bow);

            Assert.IsTrue(assignment.IsValid);
            Assert.AreEqual(bow, assignment.LeftHand);
            Assert.IsNull(assignment.RightHand, "Both swords must be cleared when equipping Bow");
        }

        [Test]
        public void Unequip_Arrow_KeepsBowInLeftHand()
        {
            var bow   = CreateItem<BowData>("Reflex Bow");
            var arrow = CreateItem<OffHandData>("Wooden Arrow"); arrow.SubType = OffHandType.Arrow;

            var assignment = _service.SmartUnequip(bow, arrow, arrow);

            Assert.IsTrue(assignment.IsValid);
            Assert.AreEqual(bow, assignment.LeftHand, "Bow should stay when only Arrow is removed");
            Assert.IsNull(assignment.RightHand);
        }

        [Test]
        public void DualWield_UnequipMainSword_PromotesDualSword()
        {
            var sword1 = CreateItem<OneHandedWeaponData>("Iron Sword");
            var sword2 = CreateItem<OneHandedWeaponData>("Steel Sword");

            // State: sword1 (left/dual), sword2 (right/main) — remove main
            var assignment = _service.SmartUnequip(sword1, sword2, sword2);

            Assert.IsTrue(assignment.IsValid);
            Assert.IsNull(assignment.LeftHand);
            Assert.AreEqual(sword1, assignment.RightHand, "Dual-wield sword should promote to main hand");
        }

        [Test]
        public void Shield_EquippedWithSword_SwordAndShieldSetup()
        {
            var sword  = CreateItem<OneHandedWeaponData>("Iron Sword");
            var shield = CreateItem<OffHandData>("Wooden Shield"); shield.SubType = OffHandType.Shield;

            // State: left=null, right=Sword → equip Shield
            var assignment = _service.SmartEquip(null, sword, shield);

            Assert.IsTrue(assignment.IsValid);
            Assert.AreEqual(shield, assignment.LeftHand, "Shield should go left hand");
            Assert.AreEqual(sword,  assignment.RightHand, "Sword stays right hand");
        }
    }
}
