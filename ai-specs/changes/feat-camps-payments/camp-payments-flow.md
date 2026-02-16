# Camps payments flow

## Objective

As a user, I want to be able to register for camps and make payments so that I can participate in the activities offered by the camps.

## User story

As a user, I want to make payments for the camps I have registered for. The payment process should be simple and secure, allowing me to easily complete my transactions.

Payments can be made through bank transfers until now, but we want to implement a new payment flow that allows users to make payments through a payment gateway, such as RedSys, using Bank TPV. This will provide users with more convenient and secure payment options and will also streamline the payment process for both users and administrators.

Payments are divided into two parts: the first one is half of the total amount, which is paid at the time of registration, and the second half is paid later, before the camp starts. This allows users to secure their spot in the camp while also giving them some flexibility in managing their payments.

Guest users can also register for camps and make payments, but they will need to provide their email address during the registration process. This will allow us to send them payment confirmations and other relevant information about the camp.

## Acceptance criteria

- The system must allow users to register for camps and select the payment method they prefer.
- The system must integrate with a payment gateway (e.g., RedSys) to process payments securely.
- The system must provide users with a confirmation of their payment and registration for the camp.
- The system must allow administrators to view and manage payments made by users for the camps.
- The system must handle any errors that may occur during the payment process and show an appropriate error message to the user.
