using System;

namespace Diceforge.Core
{
    [Serializable]
    public sealed class RulesetConfig
    {
        // MVP: Long / SameDirectionLoop / ChipOnly / 9
        // Интерпретация для старта:
        // - Loop: поле кольцом
        // - SameDirection: оба игрока двигаются "вперёд" по кольцу
        // - 9: размер кольца (0..8)
        // - ChipOnly: есть действие "поставить фишку", которое мешает движению (движение тоже есть, иначе матч не едет)

        public int ringSize = 9;

        // шаги за ход: 0..maxStep
        public int maxStep = 2;

        // разрешать нулевой шаг (Step(0))
        public bool allowZeroStep = false;

        // сколько фишек у игрока в запасе на матч
        public int chipsPerPlayer = 3;

        // фишку нельзя ставить на клетки игроков
        public bool allowChipOnPlayers = false;

        // сколько нейтральных фишек поставить на поле в начале матча
        public int startChipsOnBoard = 0;

        // ставить стартовые фишки случайно (иначе заполняем по порядку)
        public bool startChipsRandom = true;

        // дополнительный сдвиг сидов для стартовой расстановки
        public int startChipsSeedOffset = 1337;

        // лимит ходов на всякий случай
        public int maxTurns = 60;

        // сид для повторяемости боя
        public int randomSeed = 12345;

        // логировать каждый ход
        public bool verboseLog = true;

        public void Validate()
        {
            ringSize = Math.Clamp(ringSize, 3, 99);
            maxStep = Math.Clamp(maxStep, 0, 10);
            chipsPerPlayer = Math.Clamp(chipsPerPlayer, 0, 99);
            startChipsOnBoard = Math.Clamp(startChipsOnBoard, 0, ringSize - 2);
            maxTurns = Math.Clamp(maxTurns, 1, 9999);
        }
    }
}
