"use client";
import { useEffect, useState } from "react";
import { api } from "@/lib/serviceops-api";
import { Form } from "./record-form";
export function PasswordManagement({
  administrator,
  onChanged,
  mode = "both",
  nameOnly = false,
}: {
  administrator: boolean;
  onChanged: () => void;
  mode?: "profile" | "security" | "both";
  nameOnly?: boolean;
}) {
  const [message, setMessage] = useState("");
  const [profile, setProfile] = useState<{
    fullName: string;
    email: string;
    phoneNumber: string | null;
  } | null>(null);
  const [profileError, setProfileError] = useState("");
  useEffect(() => {
    if (mode === "security") return;
    let active = true;
    api("auth/profile")
      .then((value) => {
        if (active) setProfile(value);
      })
      .catch((error) => {
        if (active) setProfileError(error.message);
      });
    return () => {
      active = false;
    };
  }, [mode]);
  return (
    <section className="panel">
      {mode !== "security" && (
        <>
          <h2>My profile</h2>
          <p className="muted">
            Update your account name{nameOnly ? "" : " and phone number"}.
            Saving ends existing sessions; sign in again afterwards. Email and
            role changes require an Administrator.
          </p>
          {profileError && (
            <p className="error" role="alert">
              {profileError}
            </p>
          )}
          {profile && (
            <>
              <p>{profile.email}</p>
              <Form
                key="profile"
                initial={profile}
                fields={[
                  { key: "fullName", label: "Full name", required: true },
                  ...(!nameOnly
                    ? [
                        {
                          key: "phoneNumber",
                          label: "Phone number",
                          type: "tel",
                        },
                      ]
                    : []),
                ]}
                submit="Save my profile"
                onSubmit={async (value) => {
                  // Contact details may have been edited on the customer card since mount.
                  const current = nameOnly
                    ? await api("auth/profile")
                    : profile;
                  await api("auth/profile", "PUT", {
                    ...value,
                    ...(nameOnly ? { phoneNumber: current.phoneNumber } : {}),
                  });
                  await api("logout", "POST");
                  onChanged();
                }}
              />
            </>
          )}
        </>
      )}
      {mode !== "profile" && (
        <>
          <h2>Change password</h2>
          <p className="muted">
            Changing a password invalidates existing tokens. Sign in again
            afterwards. Choose a unique password of at least 12 characters.
          </p>
          {message && (
            <p role="status" className="success">
              {message}
            </p>
          )}
          <Form
            key="change"
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
            submit="Change password"
            onSubmit={async (v) => {
              await api("auth/password", "POST", v);
              onChanged();
            }}
          />
          {administrator && (
            <>
              <h2>Administrator password reset</h2>
              <p className="muted">
                Reset another user&apos;s password and deliver it privately. The
                API invalidates their old tokens. Use Change password for your
                own account.
              </p>
              <Form
                key="reset"
                fields={[
                  {
                    key: "userId",
                    label: "User ID",
                    type: "number",
                    required: true,
                  },
                  {
                    key: "newPassword",
                    label: "Reset password",
                    type: "password",
                    required: true,
                  },
                  {
                    key: "confirmNewPassword",
                    label: "Confirm Reset Password",
                    type: "password",
                    required: true,
                    matches: "newPassword",
                  },
                ]}
                submit="Reset user password"
                onSubmit={async (v) => {
                  await api(`users/${v.userId}/password`, "POST", {
                    newPassword: v.newPassword,
                    confirmNewPassword: v.confirmNewPassword,
                  });
                  setMessage(
                    "Password reset. The user must sign in with the new password.",
                  );
                }}
              />
            </>
          )}
        </>
      )}
    </section>
  );
}
