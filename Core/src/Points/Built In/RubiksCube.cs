﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LabFusion.Points {
    public class RubiksCube : PointItem {
        public override string Title => "Rubik's Cube";

        public override string Author => BitEconomy.InternalAuthor;

        public override string Description => "Sample Text";

        public override int Price => 0;
    }
}