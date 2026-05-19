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
    }
}
