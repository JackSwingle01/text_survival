using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_survival.Actors
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    namespace text_survival.Actors
    {
        public static class BodyPartFactory
        {

            public static BodyPart CreateHumanBody(string name)
            {
                BodyPart body = new BodyPart(name, 100, true);
                body.AddPart(CreateHead());
                body.AddPart(CreateTorso());
                body.AddPart(CreateArm("Left Arm"));
                body.AddPart(CreateArm("Right Arm"));
                body.AddPart(CreateLeg("Left Leg"));
                body.AddPart(CreateLeg("Right Leg"));
                return body;
            }
            public static BodyPart CreateHead()
            {
                BodyPart head = new BodyPart("Head", 50, true);
                head.AddPart(CreateBrain());
                head.AddPart(CreateEyes());
                head.AddPart(CreateMouth());
                head.AddPart(CreateEar("Left Ear"));
                head.AddPart(CreateEar("Right Ear"));
                return head;
            }

            public static BodyPart CreateTorso()
            {
                BodyPart torso = new BodyPart("Torso", 70, true);
                torso.AddPart(CreateLungs());
                torso.AddPart(CreateHeart());
                torso.AddPart(CreateStomach());
                torso.AddPart(CreateLiver());
                
                return torso;
            }

            public static BodyPart CreateArm(string name = "Arm")
            {
                BodyPart arm = new BodyPart(name, 30, false);
                arm.AddPart(CreateHand());
                return arm;
            }

            public static BodyPart CreateHand(string name = "Hand")
            {
                BodyPart hand = new BodyPart(name, 15, false);
                hand.AddPart(CreateFinger("Thumb"));
                for (int i = 1; i < 5; i++)
                {
                    hand.AddPart(CreateFinger($"Finger {i}"));
                }
                return hand;
            }

            public static BodyPart CreateFinger(string name = "Finger")
            {
                return new BodyPart(name, 5, false);
            }

            public static BodyPart CreateLeg(string name = "Leg")
            {
                return new BodyPart(name, 30, false);
            }

            public static BodyPart CreateBrain()
            {
                return new BodyPart("Brain", 10, true);
            }

            public static BodyPart CreateEyes()
            {
                BodyPart eyes = new BodyPart("Eyes", 10, false);
                eyes.AddPart(CreateEye("Left Eye"));
                eyes.AddPart(CreateEye("Right Eye"));
                return eyes;
            }

            public static BodyPart CreateEye(string name = "Eye")
            {
                return new BodyPart(name, 5, false);
            }

            public static BodyPart CreateMouth()
            {
                return new BodyPart("Mouth", 10, false);
            }

            public static BodyPart CreateLungs()
            {
                return new BodyPart("Lungs", 20, true);
            }

            public static BodyPart CreateEar(string name = "Ear")
            {
                return new BodyPart(name, 5, false);
            }

            public static BodyPart CreateHeart()
            {
                return new BodyPart("Heart", 15, true);
            }

            public static BodyPart CreateStomach()
            {
                return new BodyPart("Stomach", 10, true);
            }

            public static BodyPart CreateLiver()
            {
                return new BodyPart("Liver", 10, true);
            }



            // ... other body part creation methods can follow the same pattern
        }
    }

}
