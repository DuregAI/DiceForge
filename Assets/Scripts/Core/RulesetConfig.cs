using System;

namespace Diceforge.Core
{
    [Serializable]
    public sealed class RulesetConfig
    {
        // MVP: Long / SameDirectionLoop / BlockOnly / 9
        // Интерпретация для старта:
        // - Loop: поле кольцом
        // - SameDirection: оба игрока двигаются "вперёд" по кольцу
        // - 9: размер кольца (0..8)
        // - BlockOnly: есть действие "поставить блок", которое мешает движению (движение тоже есть, иначе матч не едет)

        public int ringSize = 9;

        // шаги за ход: 0..maxStep
        public int maxStep = 2;

        // сколько блоков у игрока на матч
        public int blocksPerPlayer = 3;

        // блок нельзя ставить на клетки игроков
        public bool allowBlockOnPlayers = false;

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
            blocksPerPlayer = Math.Clamp(blocksPerPlayer, 0, 99);
            maxTurns = Math.Clamp(maxTurns, 1, 9999);
        }
    }
}
