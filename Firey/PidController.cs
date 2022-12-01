using System.Diagnostics.Metrics;

namespace Firey
{
    public class PidController
    {
        private readonly float Kp;
        private readonly float Ki;
        private readonly float Kd;
        private readonly float limitMin;
        private readonly float limitMax;
        private readonly float lpfTimeConstantTau;

        float integrator;
        float differentiator;

        float previousError;
        float previousMeasurement;

        float output;

        public PidController(float kp, float ki, float kd, float min = -100, float max = 100, float lpfTau = 0.2f)
        {
            this.Kp = kp;
            this.Ki = ki;
            this.Kd = kd;
            this.limitMin = min;
            this.limitMax = max;
            this.lpfTimeConstantTau = lpfTau;
        }

        public float Update(float setpoint, float currentMeasurement, float deltaTime)
        {
            var error = setpoint - currentMeasurement;

            var proportional = Kp * error;

            integrator = integrator + 0.5f * Ki * deltaTime * (error + previousError);

            // dynamic integrator clamping (anti-windup)
            var limitMaxI = limitMax > proportional ? limitMax - proportional : 0;
            var limitMinI = limitMin < proportional ? limitMin - proportional : 0;
            integrator = Math.Clamp(integrator, limitMinI, limitMaxI);

            // band limited differentiator, derivative on measurement
            differentiator = (2f * Kd * (currentMeasurement - previousMeasurement)
                + (2f * lpfTimeConstantTau - deltaTime) * differentiator)
                / (2f * lpfTimeConstantTau + deltaTime);

            // sum and clamp to limits
            output = Math.Clamp(proportional + integrator + differentiator, limitMin, limitMax);

            previousError = error;
            previousMeasurement = currentMeasurement;

            return output;
        }
    }
}
