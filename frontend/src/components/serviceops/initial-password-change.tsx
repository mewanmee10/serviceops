"use client";
import { api } from "@/lib/serviceops-api";
import { Brand } from "./brand";
import { Form } from "./record-form";

export function InitialPasswordChange({
  email,
  onChanged,
}: {
  email: string;
  onChanged: () => void;
}) {
  return (
    <main className="login">
      <div className="login-top">
        <Brand />
      </div>
      <section
        className="panel"
        style={{ maxWidth: 560, margin: "32px auto", width: "100%" }}
      >
        <p className="eyebrow">ACCOUNT SECURITY</p>
        <h1>Set new password</h1>
        <p>
          Your initial password must be replaced before you can access
          ServiceOps.
        </p>
        <p className="muted">
          {email}. Choose a unique password of at least 12 characters. Saving
          signs you out; sign in again with your new password.
        </p>
        <Form
          fields={[
            {
              key: "currentPassword",
              label: "Current password",
              type: "password",
              required: true,
            },
            {
              key: "newPassword",
              label: "New password",
              type: "password",
              required: true,
            },
            {
              key: "confirmNewPassword",
              label: "Confirm New Password",
              type: "password",
              required: true,
              matches: "newPassword",
            },
          ]}
          submit="Set new password"
          onSubmit={async (value) => {
            await api("auth/password", "POST", value);
            onChanged();
          }}
        />
        <button
          type="button"
          style={{ marginTop: 18 }}
          onClick={async () => {
            await api("logout", "POST");
            onChanged();
          }}
        >
          Sign out
        </button>
      </section>
    </main>
  );
}
