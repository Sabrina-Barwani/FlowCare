namespace FlowCare.Api.Entities
{
    public class Enums
    {
        public enum UserRole
        {
            Admin = 1,
            BranchManager = 2,
            Staff = 3,
            Customer = 4
        }

        public enum AppointmentStatus
        {
            Booked = 1,
            Cancelled = 2,
            CheckedIn = 3,
            NoShow = 4,
            Completed = 5
        }

        public enum AuditActionType
        {
            AppointmentCreated = 1,
            AppointmentRescheduled = 2,
            AppointmentCancelled = 3,
            SlotCreated = 4,
            SlotUpdated = 5,
            SlotSoftDeleted = 6,
            SlotHardDeleted = 7,
            StaffServiceAssignmentChanged = 8,
            RetentionDaysUpdated = 9,
            AppointmentStatusUpdated = 10
        }
    }
}
