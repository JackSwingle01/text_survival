namespace text_survival.Actors
{
    public static class BodyPartFactory
    {
        public static BodyPart CreateHumanBody(string name, double hp)
        {
            BodyPart body = new BodyPart(name, hp, true);
            body.AddPart(CreateHead(hp / 4));
            body.AddPart(CreateTorso(hp / 2));
            body.AddPart(CreateShoulder(hp / 4, "Left Shoulder"));
            body.AddPart(CreateShoulder(hp / 4, "Right Shoulder"));
            body.AddPart(CreateLeg(hp / 3, "Left Leg"));
            body.AddPart(CreateLeg(hp / 3, "Right Leg"));
            return body;
        }

        public static BodyPart CreateAnimalBody(string name, double hp)
        {
            BodyPart body = new BodyPart(name, hp, true);
            if (name == "Wolf")
            {
                body.AddPart(CreateHead(hp / 4));
                body.AddPart(CreateTorso(hp / 2));
                body.AddPart(CreateLeg(hp / 3, "Front Left Leg"));
                body.AddPart(CreateLeg(hp / 3, "Front Right Leg"));
                body.AddPart(CreateLeg(hp / 3, "Rear Left Leg"));
                body.AddPart(CreateLeg(hp / 3, "Rear Right Leg"));
            }
            else if (name == "Mammoth")
            {
                body.AddPart(CreateHead(hp / 4));
                body.AddPart(CreateTorso(hp / 2));
                body.AddPart(CreateLeg(hp / 3, "Front Left Leg"));
                body.AddPart(CreateLeg(hp / 3, "Front Right Leg"));
                body.AddPart(CreateLeg(hp / 3, "Rear Left Leg"));
                body.AddPart(CreateLeg(hp / 3, "Rear Right Leg"));
            }
            else
            {
                body.AddPart(CreateHead(hp / 4));
                body.AddPart(CreateTorso(hp / 2));
                body.AddPart(CreateLeg(hp / 3, "Left Leg"));
                body.AddPart(CreateLeg(hp / 3, "Right Leg"));
            }
            return body;
        }

        public static BodyPart CreateGenericBody(string name, double hp)
        {
            return new BodyPart(name, hp, true);
        }

        public static BodyPart CreateHead(double hp)
        {
            BodyPart head = new BodyPart("Head", hp, true);
            head.AddPart(CreateBrain(hp / 2));
            head.AddPart(CreateEye(hp / 8, "Right Eye"));
            head.AddPart(CreateEye(hp / 8, "Left Eye"));
            head.AddPart(CreateMouth(hp / 8));
            head.AddPart(CreateEar(hp / 8, "Left Ear"));
            head.AddPart(CreateEar(hp / 8, "Right Ear"));
            head.AddPart(CreateJaw(hp / 8));
            head.AddPart(CreateTongue(hp / 8));
            return head;
        }

        public static BodyPart CreateTorso(double hp)
        {
            BodyPart torso = new BodyPart("Torso", hp, true);
            torso.AddPart(CreateLungs(hp / 2));
            torso.AddPart(CreateHeart(hp / 2));
            torso.AddPart(CreateStomach(hp / 2));
            torso.AddPart(CreateLiver(hp / 2));
            torso.AddPart(CreateKidney(hp / 2, "Left Kidney"));
            torso.AddPart(CreateKidney(hp / 2, "Right Kidney"));
            torso.AddPart(CreateSpine(hp / 2));
            torso.AddPart(CreateRibcage(hp / 2));
            torso.AddPart(CreateSternum(hp / 2));
            torso.AddPart(CreatePelvis(hp / 2));
            return torso;
        }

        public static BodyPart CreateShoulder(double hp, string name = "Shoulder")
        {
            BodyPart shoulder = new BodyPart(name, hp, false);
            shoulder.AddPart(CreateClavicle(hp / 2));
            shoulder.AddPart(CreateArm(hp / 2, name.Replace("Shoulder", "Arm")));
            shoulder.AddCapacity("Manipulation", 0.5);
            return shoulder;
        }

        public static BodyPart CreateArm(double hp, string name = "Arm")
        {
            BodyPart arm = new BodyPart(name, hp, false);
            arm.AddPart(CreateHand(hp / 2, name.Replace("Arm", "Hand")));
            arm.AddPart(CreateHumerus(hp / 2));
            arm.AddPart(CreateRadius(hp / 2));
            arm.AddCapacity("Manipulation", 0.5);
            return arm;
        }

        public static BodyPart CreateHand(double hp, string name = "Hand")
        {
            BodyPart hand = new BodyPart(name, hp, false);
            hand.AddPart(CreateFinger(hp / 5, name.Replace("Hand", "Thumb")));
            hand.AddPart(CreateFinger(hp / 5, name.Replace("Hand", "Index Finger")));
            hand.AddPart(CreateFinger(hp / 5, name.Replace("Hand", "Middle Finger")));
            hand.AddPart(CreateFinger(hp / 5, name.Replace("Hand", "Ring Finger")));
            hand.AddPart(CreateFinger(hp / 5, name.Replace("Hand", "Pinky")));
            hand.AddCapacity("Manipulation", 0.5);
            return hand;
        }

        public static BodyPart CreateFinger(double hp, string name = "Finger")
        {
            BodyPart finger = new BodyPart(name, hp, false);
            finger.AddCapacity("Manipulation", 0.08);
            return finger;
        }

        public static BodyPart CreateLeg(double hp, string name = "Leg")
        {
            BodyPart leg = new BodyPart(name, hp, false);
            leg.AddPart(CreateFoot(hp / 2, name.Replace("Leg", "Foot")));
            leg.AddPart(CreateFemur(hp / 2));
            leg.AddPart(CreateTibia(hp / 2));
            leg.AddCapacity("Moving", 0.5);
            return leg;
        }

        public static BodyPart CreateFoot(double hp, string name = "Foot")
        {
            BodyPart foot = new BodyPart(name, hp, false);
            foot.AddPart(CreateToe(hp / 5, name.Replace("Foot", "Big Toe")));
            foot.AddPart(CreateToe(hp / 5, name.Replace("Foot", "Second Toe")));
            foot.AddPart(CreateToe(hp / 5, name.Replace("Foot", "Middle Toe")));
            foot.AddPart(CreateToe(hp / 5, name.Replace("Foot", "Fourth Toe")));
            foot.AddPart(CreateToe(hp / 5, name.Replace("Foot", "Little Toe")));
            foot.AddCapacity("Moving", 0.5);
            return foot;
        }

        public static BodyPart CreateToe(double hp, string name = "Toe")
        {
            BodyPart toe = new BodyPart(name, hp, false);
            toe.AddCapacity("Moving", 0.04);
            return toe;
        }

        public static BodyPart CreateBrain(double hp)
        {
            BodyPart brain = new BodyPart("Brain", hp, true);
            brain.AddCapacity("Consciousness", 1.0);
            return brain;
        }

        public static BodyPart CreateEye(double hp, string name = "Eye")
        {
            BodyPart eye = new BodyPart(name, hp, false);
            eye.AddCapacity("Sight", 0.5);
            return eye;
        }

        public static BodyPart CreateMouth(double hp)
        {
            BodyPart mouth = new BodyPart("Mouth", hp, false);
            mouth.AddCapacity("Eating", 0.5);
            mouth.AddCapacity("Talking", 0.5);
            return mouth;
        }

        public static BodyPart CreateJaw(double hp)
        {
            BodyPart jaw = new BodyPart("Jaw", hp, false);
            jaw.AddCapacity("Eating", 0.5);
            jaw.AddCapacity("Talking", 0.5);
            return jaw;
        }

        public static BodyPart CreateTongue(double hp)
        {
            BodyPart tongue = new BodyPart("Tongue", hp, false);
            tongue.AddCapacity("Talking", 0.5);
            return tongue;
        }

        public static BodyPart CreateLungs(double hp)
        {
            BodyPart lungs = new BodyPart("Lungs", hp, true);
            lungs.AddCapacity("Breathing", 0.5);
            return lungs;
        }

        public static BodyPart CreateEar(double hp, string name = "Ear")
        {
            BodyPart ear = new BodyPart(name, hp, false);
            ear.AddCapacity("Hearing", 0.5);
            return ear;
        }

        public static BodyPart CreateHeart(double hp)
        {
            BodyPart heart = new BodyPart("Heart", hp, true);
            heart.AddCapacity("BloodPumping", 1.0);
            return heart;
        }

        public static BodyPart CreateStomach(double hp)
        {
            BodyPart stomach = new BodyPart("Stomach", hp, true);
            stomach.AddCapacity("Digestion", 0.5);
            return stomach;
        }

        public static BodyPart CreateLiver(double hp)
        {
            BodyPart liver = new BodyPart("Liver", hp, true);
            liver.AddCapacity("Digestion", 0.5);
            return liver;
        }

        public static BodyPart CreateKidney(double hp, string name = "Kidney")
        {
            BodyPart kidney = new BodyPart(name, hp, true);
            kidney.AddCapacity("BloodFiltration", 0.5);
            return kidney;
        }

        public static BodyPart CreateSpine(double hp)
        {
            BodyPart spine = new BodyPart("Spine", hp, false);
            spine.AddCapacity("Moving", 1.0);
            return spine;
        }

        public static BodyPart CreateRibcage(double hp)
        {
            BodyPart ribcage = new BodyPart("Ribcage", hp, false);
            ribcage.AddCapacity("Breathing", 0.5);
            return ribcage;
        }

        public static BodyPart CreateSternum(double hp)
        {
            BodyPart sternum = new BodyPart("Sternum", hp, false);
            sternum.AddCapacity("Breathing", 0.5);
            return sternum;
        }

        public static BodyPart CreatePelvis(double hp)
        {
            BodyPart pelvis = new BodyPart("Pelvis", hp, false);
            pelvis.AddCapacity("Moving", 1.0);
            return pelvis;
        }

        public static BodyPart CreateClavicle(double hp)
        {
            BodyPart clavicle = new BodyPart("Clavicle", hp, false);
            clavicle.AddCapacity("Manipulation", 0.5);
            return clavicle;
        }

        public static BodyPart CreateHumerus(double hp)
        {
            BodyPart humerus = new BodyPart("Humerus", hp, false);
            humerus.AddCapacity("Manipulation", 0.5);
            return humerus;
        }

        public static BodyPart CreateRadius(double hp)
        {
            BodyPart radius = new BodyPart("Radius", hp, false);
            radius.AddCapacity("Manipulation", 0.5);
            return radius;
        }

        public static BodyPart CreateFemur(double hp)
        {
            BodyPart femur = new BodyPart("Femur", hp, false);
            femur.AddCapacity("Moving", 0.5);
            return femur;
        }

        public static BodyPart CreateTibia(double hp)
        {
            BodyPart tibia = new BodyPart("Tibia", hp, false);
            tibia.AddCapacity("Moving", 0.5);
            return tibia;
        }
    }
}