#  Enterprise Email Service Design

 

## Background

The purpose of this design is to create an enterprise-grade email service capable of sending HTML emails at scale. The service is targeted at large enterprises that require a robust, scalable solution for sending transactional and marketing emails. In addition to basic email delivery, the service will offer features like template customization and detailed email tracking, which are critical for businesses to manage and optimize their email communication strategies.

## Requirements

The system must fulfill the following requirements:

*Must-Have:*
- Ability to send HTML emails.
- Support for multiple pricing plans to accommodate varying levels of email volume and features.
- Template management system with default templates that users can customize.
- Email tracking to monitor the status of sent emails, including whether they were delivered, failed, opened, or clicked.
  
*Should-Have:*
- API access for integration with other enterprise systems.
- Detailed analytics dashboard for tracking email performance.

*Could-Have:*
- A/B testing for email campaigns.
- Multi-language support for email templates.

*Won't-Have:*
- Direct support for non-email communication channels (e.g., SMS, push notifications) in the initial version.



To start, I'll break down the **Method** section into several key areas:
1. **High-Level Architecture**
2. **Components and Their Responsibilities**
3. **Database Design**
4. **Email Sending Workflow**
5. **Template Management**

Let's begin with the high-level architecture.

### High-Level Architecture
The email service will consist of several core components, each responsible for a specific aspect of the service. These components will be designed to scale horizontally to handle large volumes of emails, with a focus on reliability and fault tolerance.

![Architecture](./Email%20Service%20Architecture.png)



### Components Overview

1. **API Gateway**:
   - Exposes RESTful APIs for email sending, template management, and tracking.
   - Handles authentication and rate-limiting.
  
2. **User Management Service**:
   - Handles user authentication, authorization, and management. The RDBMS database stores user-related data, such as credentials and plan details.

3. **Template Service**:
   - Manages default templates and allows users to customize them.
   - Stores template data and provides APIs for CRUD operations.

4. **Email Service**:
   - Responsible for processing and sending emails.
   - Interfaces with an internal message queue to ensure reliable email delivery.
   - Interfaces with third-party SMTP servers or proprietary email sending infrastructure.
   - Consumes tasks from the queue and sends emails using the Email Gateway.
   - Ensures retries on failures and logs the status for tracking.
   - Monitors the status of sent emails (delivered, failed, opened, clicked).
   - Provides APIs to fetch email status and generates analytics reports.

   

### Database Design

 To design the database, we need to consider the specific requirements of each component and choose the appropriate type of database for storing and querying the data efficiently. Given the system's needs, we’ll use a combination of relational and non-relational databases to optimize for scalability, performance, and data integrity.

#### 1. **User Management Database (SQL)**
   - **Purpose**: Used for storing structured data where relationships between entities are critical, such as user information, email templates, and tracking data. A relational database like PostgreSQL or MySQL is well-suited for this purpose.

   ![Database](./User%20Management%20Database%20(SQL).png)
   
   - **User and Plan Database**:

   ```plaintext
   Table Users {
  id int [pk, increment] // Primary key
  name varchar
  email varchar [unique]
  password_hash varchar
  created_at timestamp
  updated_at timestamp
}

Table Plans {
  id int [pk, increment] // Primary key
  name varchar
  email_limit int // Maximum emails allowed per month
  features json // Stores plan-specific features in JSON
  price decimal // Plan price
  duration_in_days int // Plan duration in days (e.g., 30 for a monthly plan)
  created_at timestamp
  updated_at timestamp
}

Table UserPlans {
  id int [pk, increment] // Primary key
  user_id int [ref: > Users.id] // Foreign key to Users
  plan_id int [ref: > Plans.id] // Foreign key to Plans
  start_date timestamp // Plan start date
  end_date timestamp // Plan end date
  auto_renewal boolean // Indicates if the plan will auto-renew
  status varchar [note: 'active, expired, cancelled'] // Plan status
  created_at timestamp
  updated_at timestamp
}

Table PaymentTransactions {
  id int [pk, increment] // Primary key
  user_plan_id int [ref: > UserPlans.id] // Foreign key to UserPlans
  amount decimal // Payment amount
  payment_method varchar [note: 'credit_card, paypal, etc.'] // Payment method used
  transaction_date timestamp // Date of the transaction
  transaction_status varchar [note: 'completed, failed, pending'] // Status of the transaction
  created_at timestamp
  updated_at timestamp
}

Table AutoPayments {
  id int [pk, increment] // Primary key
  user_plan_id int [ref: > UserPlans.id] // Foreign key to UserPlans
  payment_method varchar [note: 'credit_card, paypal, etc.'] // Payment method to be used
  next_payment_date timestamp // Date for the next scheduled payment
  status varchar [note: 'active, inactive'] // Auto-payment status
  created_at timestamp
  updated_at timestamp
}
   ```
#### 2. **Template Management Database (NoSQL)**
   - **Purpose**: For the Template Management Database, which will be a NoSQL database, we'll design collections to store default templates and customer-customized templates. The design will be optimized for flexibility, allowing for different structures of template content and metadata.
  
   - **Templates Collection:** Stores all templates, including default system templates and customer-specific customized templates.
```json
{
  "_id": ObjectId("..."), // Unique identifier
  "name": "Welcome Email", // Template name
  "type": "default", // "default" for system templates, "custom" for user-customized templates
  "user_id": null, // Null for default templates, or ObjectId reference to Users collection for custom templates
  "content": {
    "subject": "Welcome to Our Service!",
    "body": "<html><body><h1>Welcome, {{name}}!</h1></body></html>" // HTML content with placeholders
  },
  "placeholders": ["name", "date"], // List of placeholders in the template
  "created_at": ISODate("..."),
  "updated_at": ISODate("...")
}
```
 - **TemplateVersions Collection:** Stores version history for each template, allowing for rollback or auditing of changes.
```json
{
  "_id": ObjectId("..."), // Unique identifier
  "template_id": ObjectId("..."), // Reference to the Templates collection
  "version": 2, // Version number
  "content": {
    "subject": "Updated Welcome Email",
    "body": "<html><body><h1>Welcome, {{name}}!</h1><p>Thank you for joining us.</p></body></html>"
  },
  "placeholders": ["name", "date"],
  "created_at": ISODate("...")
}
```

 - **TemplateUsageStats Collection:** Tracks usage statistics for templates, including how many times each template has been used and by whom.
```json
{
  "_id": ObjectId("..."), // Unique identifier
  "template_id": ObjectId("..."), // Reference to the Templates collection
  "user_id": ObjectId("..."), // Reference to the user who used the template
  "usage_count": 5, // Number of times this template has been used
  "last_used_at": ISODate("...") // Timestamp of the last usage
}
```
#### 3. **Email Database (SQL)**
   - **Purpose**: This service handles the processing and sending of emails, logging their statuses, and providing analytics.

```plaintext
Table Emails {
  id int [pk, increment] // Primary key
  user_id int [ref: > Users.id] // Foreign key to Users (if applicable)
  recipient_email varchar // Recipient's email address
  subject varchar // Email subject
  content text // Email content (could be a reference if stored elsewhere)
  status varchar [note: 'queued, sent, failed, delivered, opened, clicked'] // Current status of the email
  retries int // Number of retries in case of failure
  created_at timestamp
  updated_at timestamp
}

```

**Note:** we use CDN tp store send email content or and images/css etc.


## Email Workflow

 ![Workflow](./email%20service.png)

 ```plantuml
@startuml
skinparam componentStyle rectangle

User -> WebApp : Manage Templates, Track Emails
User -> API : Send Emails

API -> UserManagementService : Authenticate Request
UserManagementService -> UserDB : Verify User and Plan

API -> TemplateService : Fetch Template
TemplateService -> TemplateDB : Retrieve Template Data

API -> EmailService : Queue Email for Delivery
EmailService -> RabbitMQ : Push Email Data

EmailService -> EmailGateway : Process and Send Email
EmailService -> EmailDB : Log Email Status and Events

WebApp -> EmailService : Fetch Email Status

@enduml
```

 1. **Request Reception**
   - The Email Service receives a request to send an email via its API.
   - The request includes details such as the recipient's email, subject, content, and optional template ID.

2. **User and Plan Validation**
   - The Email Service verifies the user's identity and checks their subscription plan to ensure they are allowed to send emails.
   - If validation fails (e.g., plan limit exceeded), the service responds with an error.

3. **Template Processing (Optional)**
   - If a template ID is provided, the service fetches the template from the Template Management Database (NoSQL).
   - Placeholder values in the template are replaced with actual data from the request.
   - The final email content is generated.

4. **Queue Email for Delivery**
   - The email, along with its metadata (recipient, subject, content, etc.), is queued in RabbitMQ for processing.
   - The email's initial status is set to "queued" and logged in the `Emails` table.

5. **Email Worker Fetches Task**
   - The worker service, which is part of the Email Service, fetches the email task from RabbitMQ.
   - The email status is updated to "processing" in the `Emails` table.

6. **Send Email via Email Gateway**
   - The worker processes the email task, sending the email via the Email Gateway (SMTP server or third-party service).
   - The worker handles retries in case of temporary failures, updating the retry count in the `EmailRetries` table.

7. **Update Email Status**
   - Based on the response from the Email Gateway:
     - If the email is successfully sent, the status is updated to "sent."
     - If the email fails to send, the status is updated to "failed," and retry logic is triggered if applicable.
   - The event (e.g., sent, failed) is logged in the `EmailLogs` table.

8. **Trigger Tracking Events**
   - The worker triggers tracking events for the sent email (e.g., delivered, opened, clicked) by monitoring responses from the Email Gateway.
   - These events are logged in the `EmailLogs` table and `EmailAnalytics` table for reporting purposes.

9. **Email Status Monitoring**
   - The Email Service continuously monitors the status of sent emails, updating the `Emails` and `EmailAnalytics` tables as events occur.
   - This monitoring includes checking for delivery confirmations, open rates, and click-through rates.

10. **API Response and Logging**
    - The API responds to the initial request with the email's status (e.g., queued, sent).
    - The Email Service ensures that all actions are logged for audit purposes.

11. **Analytics and Reporting**
    - The Email Service aggregates data from the `EmailAnalytics` and `EmailLogs` tables to generate reports.
    - Users can fetch these reports via the API, gaining insights into email performance.

12. **Error Handling**
    - The Email Service includes error handling at each step, ensuring that any failures are logged, retried if possible, and reported back to the user.
    - Critical errors trigger alerts for further investigation.

