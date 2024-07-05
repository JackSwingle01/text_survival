using System.Runtime.InteropServices;

namespace text_survival.Actors
{
    namespace text_survival.Actors
    {
        public static class BodyPartFactory
        {
            public static BodyPart CreateHumanBody(string name, int hp)
            {
                BodyPart body = new BodyPart(name, hp, true);
                body.AddPart(CreateHead(hp / 4));
                body.AddPart(CreateTorso(hp / 2));
                body.AddPart(CreateArm(hp / 4, "Left Arm"));
                body.AddPart(CreateArm(hp / 4, "Right Arm"));
                body.AddPart(CreateLeg(hp / 3, "Left Leg"));
                body.AddPart(CreateLeg(hp / 3, "Right Leg"));
                return body;
            }
            public static BodyPart CreateAnimalBody(string name, int hp)
            {
                BodyPart body = new BodyPart(name, hp, true);
                //to do
                return body;
            }
            public static BodyPart CreateGenericBody(string name, int hp)
            {
                BodyPart body = new BodyPart(name, hp, true);
                return body;
            }
            public static BodyPart CreateHead(int hp)
            {
                BodyPart head = new BodyPart("Head", hp, true);
                head.AddPart(CreateBrain(hp / 2));
                head.AddPart(CreateEye(hp / 8, "Right Eye"));
                head.AddPart(CreateEye(hp / 8, "Left Eye"));

                head.AddPart(CreateMouth(hp / 8));
                head.AddPart(CreateEar(hp / 8, "Left Ear"));
                head.AddPart(CreateEar(hp / 8, "Right Ear"));
                return head;
            }

            public static BodyPart CreateTorso(int hp)
            {
                BodyPart torso = new BodyPart("Torso", hp, true);
                torso.AddPart(CreateLungs(hp / 2));
                torso.AddPart(CreateHeart(hp / 2));
                torso.AddPart(CreateStomach(hp / 2));
                torso.AddPart(CreateLiver(hp / 2));

                return torso;
            }

            public static BodyPart CreateArm(int hp, string name = "Arm")
            {
                BodyPart arm = new BodyPart(name, hp, false);
                arm.AddPart(CreateHand(hp / 2));
                return arm;
            }

            public static BodyPart CreateHand(int hp, string name = "Hand")
            {
                BodyPart hand = new BodyPart(name, hp, false);
                hand.AddPart(CreateFinger(hp / 5, "Thumb"));
                for (int i = 1; i < 5; i++)
                {
                    hand.AddPart(CreateFinger(hp / 5, $"Finger {i}"));
                }
                return hand;
            }

            public static BodyPart CreateFinger(int hp, string name = "Finger")
            {
                return new BodyPart(name, hp, false);
            }

            public static BodyPart CreateLeg(int hp, string name = "Leg")
            {
                return new BodyPart(name, hp, false);
            }

            public static BodyPart CreateBrain(int hp)
            {
                return new BodyPart("Brain", hp, true);
            }

            public static BodyPart CreateEye(int hp, string name = "Eye")
            {
                return new BodyPart(name, hp, false);
            }

            public static BodyPart CreateMouth(int hp)
            {
                return new BodyPart("Mouth", hp, false);
            }

            public static BodyPart CreateLungs(int hp)
            {
                return new BodyPart("Lungs", hp, true);
            }

            public static BodyPart CreateEar(int hp, string name = "Ear")
            {
                return new BodyPart(name, hp, false);
            }

            public static BodyPart CreateHeart(int hp)
            {
                return new BodyPart("Heart", hp, true);
            }

            public static BodyPart CreateStomach(int hp)
            {
                return new BodyPart("Stomach", hp, true);
            }

            public static BodyPart CreateLiver(int hp)
            {
                return new BodyPart("Liver", hp, true);
            }



            // ... other body part creation methods can follow the same pattern
        }
    }

}
