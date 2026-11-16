"use client";
import { useCallback, useEffect, useState } from "react";
import { DedicatedRecord } from "./dedicated-record";
import { recordHref, recordResources, type RecordRoute } from "@/lib/record-routes";
import { ServiceOpsMaintenanceBackground } from "./video-background";
import { usePathname, useRouter } from "next/navigation";
import { ServiceArtwork } from "./service-artwork";
import { navigationFor, roleNavigation } from "./role-navigation";
import { Monitoring } from "./monitoring";
import { InquiryList } from "./inquiry-list";
import { CustomerProfile } from "./customer-profile";
import { ServiceHero } from "./service-hero";
import { Brand } from "./brand";
import { useSidebarPreference } from "./sidebar-preference";
import { NotificationPreview } from "./notification-preview";
import { api, type Row } from "@/lib/serviceops-api";
import { InventorySummary } from "./inventory-summary";
import { Administration } from "./administration";
import { Analytics } from "./analytics";
import { InitialPasswordChange } from "./initial-password-change";
import { PasswordManagement } from "./password-management";
import { Attachments } from "./attachments";
import { Form, type Field } from "./record-form";
import {
  Activity,
  ClipboardList,
  Wrench,
  History,
  LogOut,
  Menu,
  RefreshCw,
  ArrowUpRight,
  ShieldCheck,
  PanelLeftClose,
  PanelLeftOpen,
  X,
} from "lucide-react";

type Identity = {
  userId: string | number;
  name: string;
  email: string;
  role: string;
  permissions: string[];
  mustChangePassword: boolean;
};

const statuses = [
  "New",
  "Assigned",
  "In progress",
  "On hold",
  "Completed",
  "Closed",
];
const priorities = ["Low", "Medium", "High", "Urgent"];

function display(key: string, value: Row[string]) {
  if (value === null) return "—";
  if (key === "status") return statuses[Number(value)] ?? String(value);
  if (key === "priority") return priorities[Number(value)] ?? String(value);
  if (typeof value === "boolean") return value ? "Yes" : "No";
  if (/At$|Date$|Time$|Visit$/.test(key))
    return new Date(String(value)).toLocaleString();
  return String(value);
}
export default function Workspace({ initialSection = "dashboard", recordRoute }: { initialSection?: string; recordRoute?: RecordRoute }) {
  const [user, setUser] = useState<Identity | null>(null);
  const router = useRouter();
  const pathname = usePathname();
  const [checking, setChecking] = useState(true);
  const [notificationFilter, setNotificationFilter] = useState("all");
  const [panelVersion, setPanelVersion] = useState(0);
  const [recordPage, setRecordPage] = useState(1);
  const [section, setSection] = useState(initialSection);
  const [page, setPage] = useState(recordRoute ? recordResources[recordRoute.resource].api : Object.values(roleNavigation).flat().find(n => n.id === initialSection)?.page || initialSection);
  const [rows, setRows] = useState<Row[]>([]);
  const [stats, setStats] = useState<Row>({});
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [busy, setBusy] = useState(false);
  const [mobile, setMobile] = useState(false);
  const [sidebarCollapsed, toggleSidebar] = useSidebarPreference();
  const [search, setSearch] = useState("");
  const [inventorySort,setInventorySort]=useState("name");
  const [stockFilter,setStockFilter]=useState("all");
  const [form, setForm] = useState<{
    fields: Field[];
    path: string;
    method?: string;
    initial?: Row;
  } | null>(null);
  const [requestEvidence, setRequestEvidence] = useState<number | null>(null);
  const [detail, setDetail] = useState<Record<string, unknown> | null>(null);
  useEffect(() => {
    api("auth/me")
      .then(setUser)
      .catch(() => {})
      .finally(() => setChecking(false));
    const expire = () => {
      setUser(null);
      setMobile(false);
      setSuccess("");
      setError("");
      setRequestEvidence(null);
      setRows([]);
      setStats({});
      setDetail(null);
      setForm(null);
    };
    window.addEventListener("session-expired", expire);
    return () => window.removeEventListener("session-expired", expire);
  }, []);
  useEffect(() => {
    if (checking) return;
    // A new document installs the dashboard-only Google Maps CSP.
    if (user && pathname === "/login") window.location.replace("/dashboard");
    else if (!user && pathname.startsWith("/dashboard")) router.replace("/login");
  }, [checking, user, pathname, router]);
  const role = user?.role;
  const staff = role === "Administrator" || role === "Service Manager";
  const navigation = navigationFor(role, user?.permissions);
  const load = useCallback(async () => {
    if (!user || user.mustChangePassword || recordRoute) return;
    if (
      [
        "administration",
        "analytics",
        "security",
        "inventory-summary",
        "monitoring",
        "inquiries",
      ].includes(page)
    ) {
      setPanelVersion((v) => v + 1);
      return;
    }
    setBusy(true);
    try {
      const data = await api(page === "dashboard" ? "dashboard" : page);
      if (page === "dashboard") setStats(data);
      else setRows(data);
      setError("");
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }, [page, user, recordRoute]);
  useEffect(() => {
    if (
      !user ||
      user.mustChangePassword || recordRoute ||
      [
        "administration",
        "analytics",
        "security",
        "inventory-summary",
        "monitoring",
        "inquiries",
      ].includes(page)
    )
      return;
    let cancelled = false;
    api(page === "dashboard" ? "dashboard" : page)
      .then((data) => {
        if (cancelled) return;
        if (page === "dashboard") setStats(data);
        else setRows(data);
        setError("");
        setBusy(false);
      })
      .catch((e) => {
        if (!cancelled) {
          setError(e.message);
          setBusy(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [page, user, section, recordRoute]);
  useEffect(() => {
    if (!mobile) return;
    const sidebar = document.getElementById("workspace-navigation");
    const trigger = document.getElementById("navigation-toggle");
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const visibleButtons = () =>
      Array.from(
        sidebar?.querySelectorAll<HTMLButtonElement>("button") ?? [],
      ).filter((button) => button.getClientRects().length > 0);
    visibleButtons()[0]?.focus();
    const keyboard = (e: KeyboardEvent) => {
      if (e.key === "Escape") setMobile(false);
      if (e.key !== "Tab") return;
      const buttons = visibleButtons();
      if (!buttons?.length) return;
      const first = buttons[0],
        last = buttons[buttons.length - 1];
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    };
    const resize = () => {
      if (window.innerWidth > 900) setMobile(false);
    };
    document.addEventListener("keydown", keyboard);
    window.addEventListener("resize", resize);
    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener("keydown", keyboard);
      window.removeEventListener("resize", resize);
      trigger?.focus();
    };
  }, [mobile]);
  function selectSection(id: string) {
    const destination = navigation.find((n) => n.id === id);
    if (!destination) return;
    router.push(`/dashboard?section=${encodeURIComponent(id)}`);
    if (id === section) {
      setMobile(false);
      return;
    }
    if (
      form &&
      !window.confirm(
        "Leave this section? Unsaved record changes will be lost.",
      )
    )
      return;
    setBusy(true);
    setRows([]);
    setPage(destination.page);
    setSection(id);
    setPanelVersion((v) => v + 1);
    setRecordPage(1);
    setMobile(false);
    setForm(null);
    setDetail(null);
    setSuccess("");
    setSearch("");
    if (id === "create-request") void openCreate("requests");
  }
  async function action(path: string, method: string, body?: object) {
    setError("");
    setSuccess("");
    try {
      await api(path, method, body);
      setSuccess("Changes saved.");
      await load();
      if (detail) setDetail(await api(`work-orders/${detail.workOrderId}`));
    } catch (e) {
      setError((e as Error).message);
    }
  }
  async function options(path: string, id: string, label: string) {
    const data: Row[] = await api(path);
    return data.map((r) => ({ value: String(r[id]), label: String(r[label]) }));
  }
  async function openCreate(targetPage = page) {
    try {
      const required = (key: string, label: string, type?: string): Field => ({
        key,
        label,
        type,
        required: true,
      });
      let fields: Field[] = [];
      if (targetPage === "users")
        fields = [
          required("fullName", "Full name"),
          required("email", "Email", "email"),
          required("password", "Password (at least 12 characters)", "password"),
          {
            ...required("confirmPassword", "Confirm Password", "password"),
            matches: "password",
          },
          {
            key: "roleId",
            label: "Role",
            type: "number",
            required: true,
            options: await options("users/roles", "roleId", "roleName"),
          },
          {
            key: "customerName",
            label: "Customer name (required for Customer)",
          },
          { key: "technicianSkill", label: "Skills (required for Technician)" },
          { key: "phoneNumber", label: "Phone", type: "tel" },
        ];
      if (targetPage === "sites")
        fields = [
          {
            key: "customerId",
            label: "Customer",
            type: "number",
            required: true,
            options: await options("customers", "customerId", "customerName"),
          },
          required("siteName", "Site name"),
          required("addressLine", "Address"),
          { key: "city", label: "City" },
          { key: "province", label: "Province" },
          { key: "postalCode", label: "Postal code" },
          { key: "locationDetails", label: "Location details" },
          { key: "mapsUrl", label: "Google Maps Location Link", type: "url", maxLength: 2048, placeholder: "Paste a Google Maps location-sharing link", help: "Open Google Maps, select the service location, choose Share, and paste the location link here." },
        ];
      if (targetPage === "assets")
        fields = [
          {
            key: "siteId",
            label: "Site",
            type: "number",
            required: true,
            options: await options("sites", "siteId", "siteName"),
          },
          required("assetName", "Equipment name"),
          { key: "category", label: "Category" },
          { key: "serialNumber", label: "Serial number" },
          { key: "installationDate", label: "Installation date", type: "date" },
          { key: "warrantyInformation", label: "Warranty information" },
        ];
      if (targetPage === "requests")
        fields = [
          {
            key: "siteId",
            label: "Site",
            type: "number",
            required: true,
            options: await options("sites", "siteId", "siteName"),
          },
          {
            key: "assetId",
            label: "Equipment (must belong to site)",
            type: "number",
            options: await options("assets", "assetId", "assetName"),
          },
          required("category", "Category"),
          required("issueDescription", "Describe the issue", "textarea"),
        ];
      if (targetPage === "inventory/parts")
        fields = [
          required("partName", "Part name"),
          { key: "description", label: "Description" },
          {
            key: "lowStockLevel",
            label: "Reorder threshold",
            type: "number",
            required: true,
          },
        ];
      if (targetPage === "customers" && staff) {
        fields = [
          required("fullName", "Account name"),
          required("email", "Email", "email"),
          required(
            "password",
            "Temporary password (deliver privately)",
            "password",
          ),
          required("customerName", "Customer name"),
          { key: "phoneNumber", label: "Phone", type: "tel" },
        ];
      }
      setForm({
        fields,
        path: targetPage === "customers" ? "customers/onboard" : targetPage,
      });
    } catch (e) {
      setError((e as Error).message);
    }
  }
  async function edit(row: Row) {
    const href = recordHref(page, row, true);
    if (href) router.push(href);
  }
  async function openIssue(row: Row, operation = "issue") {
    try {
      const workOptions = await options(
        "inventory/work-orders",
        "workOrderId",
        "issueDescription",
      );
      setForm({
        path: `inventory/parts/${row.sparePartId}/${operation}`,
        fields: [
          {
            key: "quantity",
            label: "Quantity",
            type: "number",
            required: true,
          },
          {
            key: "workOrderId",
            label: "In-progress work order",
            type: "number",
            required: true,
            options: workOptions,
          },
          { key: "remarks", label: "Reason", required: true },
        ],
      });
    } catch (e) {
      setError((e as Error).message);
    }
  }
  if (checking)
    return (
      <main className="login">
        <p>Loading ServiceOps…</p>
      </main>
    );
  if (user?.mustChangePassword)
    return (
      <InitialPasswordChange
        email={user.email}
        onChanged={() => {
          setUser(null);
          setSuccess(
            "Sign in with your new password after saving, or your existing password if you signed out.",
          );
        }}
      />
    );
  if (!user)
    return (
      <main className="login">
        <div className="login-top">
          <Brand />
        </div>
        <div className="login-composition">
          <section className="login-story" aria-label="About ServiceOps">
            <p className="eyebrow">CONNECTED FIELD SERVICE</p>
            <h2>
              Every request. <br />
              One clear path
              <br />
              <span>to resolution.</span>
            </h2>
            <p className="login-story-description">
              Bring your people, equipment and service operations together. From
              the first request to the final repair, keep every detail in view.
            </p>
            <ServiceHero />
            <div className="service-path" aria-label="Service workflow">
              <div>
                <ClipboardList size={22} />
                <span>
                  <strong>Request & review</strong>
                  <small>Capture the issue. Set the priority.</small>
                </span>
              </div>
              <div>
                <Wrench size={22} />
                <span>
                  <strong>Assign & resolve</strong>
                  <small>Coordinate technicians and parts.</small>
                </span>
              </div>
              <div>
                <History size={22} />
                <span>
                  <strong>Complete & learn</strong>
                  <small>Build a reliable maintenance history.</small>
                </span>
              </div>
            </div>
            <p className="login-story-footer">
              <ShieldCheck size={17} /> Role-based access. One connected
              workspace.
            </p>
          </section>
          <section className="login-card">
            <div className="login-card-mark">
              <ShieldCheck size={24} />
            </div>
            <p className="eyebrow">FIELD SERVICE & MAINTENANCE</p>
            <h1>Welcome back</h1>
            <p className="muted">
              Sign in to coordinate service, equipment and people.
            </p>
            {success && <p role="status">{success}</p>}
            <Form
              fields={[
                {
                  key: "email",
                  label: "Email address",
                  type: "email",
                  required: true,
                },
                {
                  key: "password",
                  label: "Password",
                  type: "password",
                  required: true,
                },
              ]}
              submit="Sign in"
              onSubmit={async (v) => {
                await api("auth/login", "POST", v);
                setMobile(false);
                setSuccess("");
                setError("");
                setForm(null);
                setDetail(null);
                setRequestEvidence(null);
                setStats({});
                setRows([]);
                setBusy(true);
                setUser(await api("auth/me"));
                setPage("dashboard");
                setSection("dashboard");
              }}
            />
            <p className="muted small">
              Use an account configured by your ServiceOps administrator.
            </p>
            <p className="login-security">
              <ShieldCheck size={14} /> Secure access to your workspace
            </p>
          </section>
        </div>
        <p className="login-bottom">
          ServiceOps · Field Service & Maintenance Management
        </p>
      </main>
    );
  // Defense in depth for presentation; server ownership remains the security boundary.
  const linkedCustomers =
    role === "Customer"
      ? rows.filter((row) => String(row.userId) === String(user.userId))
      : [];
  const ownCustomer =
    linkedCustomers.length === 1 ? linkedCustomers[0] : undefined;
  const title = recordRoute ? `${recordRoute.edit ? "Edit " : ""}${recordResources[recordRoute.resource].label}` : navigation.find((n) => n.id === section)?.label || "Overview";
  const preferredColumns: Record<string, string[]> = {
    "inventory/parts": ["sparePartId","partName","description","quantityInStock","lowStockLevel"],
    customers: [
      "customerName",
      "email",
      "phoneNumber",
      "addressLine",
      "city",
      "province",
      "postalCode",
    ],
    sites: [
      "siteName",
      "addressLine",
      "city",
      "province",
      "postalCode",
      "locationDetails",
    ],
    assets: [
      "assetName",
      "siteId",
      "category",
      "serialNumber",
      "installationDate",
      "warrantyInformation",
    ],
  };
  const columns =
    preferredColumns[page] ||
    (rows.length
      ? Object.keys(rows[0])
          .filter(
            (k) =>
              ![
                "phoneNumber",
                "updatedAt",
                "jobNote",
                "description",
                "diagnosis",
                "workPerformed",
              ].includes(k),
          )
          .slice(0, 7)
      : []);
  const filtered = rows.filter(
    (r) =>
      (!(role === "Customer" && page === "work-orders") ||
        Number(r.status) >= 4) &&
      (section !== "low-stock" ||
        Number(r.quantityInStock) <= Number(r.lowStockLevel)) &&
      (page !== "inventory/parts" || stockFilter !== "low" || Number(r.quantityInStock) <= Number(r.lowStockLevel)) &&
      (page !== "notifications" ||
        notificationFilter === "all" ||
        Boolean(r.isRead) === (notificationFilter === "read")) &&
      Object.entries(r).some(([key, v]) =>
        display(key, v).toLowerCase().includes(search.toLowerCase()),
      ),
  ).sort((a,b)=>page !== "inventory/parts" ? 0 : inventorySort === "stock" ? Number(a.quantityInStock)-Number(b.quantityInStock) : String(a.partName).localeCompare(String(b.partName)));
  return (
    <>
      <div
        className={
          sidebarCollapsed ? "workspace sidebar-collapsed" : "workspace"
        }
      >
        <ServiceOpsMaintenanceBackground />
        <a className="skip-link" href="#workspace-content">
          Skip to content
        </a>
        {mobile && (
          <button
            className="navigation-overlay"
            tabIndex={-1}
            aria-label="Dismiss navigation"
            onClick={() => setMobile(false)}
          />
        )}
        <aside
          id="workspace-navigation"
          aria-label="Workspace navigation"
          className={mobile ? "sidebar glass-sidebar open" : "sidebar glass-sidebar"}
        >
          <div className="sidebar-brand">
            <Brand />
            <button
              className="sidebar-collapse"
              aria-label={
                sidebarCollapsed ? "Expand sidebar" : "Collapse sidebar"
              }
              title={sidebarCollapsed ? "Expand sidebar" : "Collapse sidebar"}
              aria-expanded={!sidebarCollapsed}
              aria-controls="workspace-navigation"
              onClick={toggleSidebar}
            >
              {sidebarCollapsed ? (
                <PanelLeftOpen size={18} />
              ) : (
                <PanelLeftClose size={18} />
              )}
            </button>
            <button
              className="sidebar-close"
              aria-label="Close navigation"
              onClick={() => setMobile(false)}
            >
              <X size={18} />
            </button>
          </div>
          <p className="eyebrow">OPERATIONS WORKSPACE</p>
          <nav aria-label="ServiceOps sections">
            {navigation.map((n) => (
              <button
                key={n.id}
                className={section === n.id ? "nav active" : "nav"}
                aria-label={n.label}
                title={sidebarCollapsed ? n.label : undefined}
                aria-current={section === n.id ? "page" : undefined}
                onClick={() => selectSection(n.id)}
              >
                <n.icon size={18} />
                <span className="navigation-label">{n.label}</span>
                <span className="collapsed-tooltip" aria-hidden="true">
                  {n.label}
                </span>
              </button>
            ))}
          </nav>
          <div className="sidebar-footer" title={`${user.name} — ${role}`}>
            <div className="user-avatar" aria-hidden="true">
              {user.name.slice(0, 1).toUpperCase()}
            </div>
            <strong>{user.name}</strong>
            <span>{role}</span>
            <button
              aria-label="Sign out"
              title="Sign out"
              onClick={async () => {
                await api("logout", "POST");
                setUser(null);
                setMobile(false);
                setSuccess("");
                setError("");
                setForm(null);
                setDetail(null);
                setRequestEvidence(null);
                setStats({});
                setRows([]);
              }}
            >
              <LogOut size={16} />{" "}
              <span className="navigation-label">Sign out</span>
            </button>
          </div>
        </aside>
        <div className="main" inert={mobile}>
          <header className="workspace-header glass-header">
            <button
              id="navigation-toggle"
              className="mobile-toggle"
              aria-label="Toggle navigation"
              aria-expanded={mobile}
              aria-controls="workspace-navigation"
              onClick={() => setMobile(!mobile)}
            >
              <Menu />
            </button>
            <span>
              Workspace <span className="muted">/ {title}</span>
            </span>
            <div className="header-actions">
              <span className="role-pill">{role}</span>
                  <NotificationPreview
                userId={user.userId}
                onView={() => selectSection("notifications")}
              />
            </div>
          </header>
          <main
            id="workspace-content"
            tabIndex={-1}
            className="content"
            key={page}
          >
            <div
              className={
                page === "dashboard"
                  ? "page-heading welcome-banner"
                  : "page-heading"
              }
            >
              <div>
                <p className="eyebrow">
                  {page === "dashboard"
                    ? "YOUR OPERATIONS AT A GLANCE"
                    : "SERVICEOPS WORKSPACE"}
                </p>
                <h1>{title}</h1>
                <p className="muted">
                  {page === "dashboard"
                    ? `Welcome back, ${user.name.split(" ")[0]}. Let’s keep service moving.`
                    : role === "Customer" && page === "customers"
                      ? "Your linked customer details and service contact information."
                      : "Live records from your ServiceOps database."}
                </p>
              </div>
              <div className="toolbar">
                <button onClick={load} aria-label="Refresh">
                  <RefreshCw size={16} /> Refresh
                </button>
                {!recordRoute && ([
                  "users",
                  "sites",
                  "assets",
                  "inventory/parts",
                  ...(staff ? ["customers"] : []),
                ].includes(page) ||
                  (page === "requests" && (role === "Customer" || staff))) && (
                  <button className="primary" onClick={() => void openCreate()}>
                    + {page === "requests" ? "New request" : "Create"}
                  </button>
                )}
              </div>
            </div>
            {error && (
              <p className="error" role="alert">
                {error}
              </p>
            )}
            {success && (
              <p className="success" role="status">
                {success}
              </p>
            )}
            {form && (
              <section className="panel">
                <div className="panel-title">
                  <h2>
                    {form.method === "PUT" ? "Edit record" : "New record"}
                  </h2>
                  <button onClick={() => setForm(null)}>Cancel</button>
                </div>
                <Form
                  key={form.path}
                  fields={form.fields}
                  initial={form.initial}
                  onSubmit={async (v) => {
                    await api(
                      form.path,
                      form.method || "POST",
                      form.method === "PUT" &&
                        ["customers", "sites", "assets"].includes(page)
                        ? {
                            ...v,
                            ...(page === "customers" && role === "Customer"
                              ? { userId: Number(user.userId) }
                              : {}),
                            replaceDetails: true,
                          }
                        : v,
                    );
                    setForm(null);
                    setSuccess("Record saved.");
                    await load();
                  }}
                />
              </section>
            )}
            {recordRoute ? <DedicatedRecord route={recordRoute} role={role!} userId={Number(user.userId)} permissions={user.permissions} /> : page === "inquiries" ? <InquiryList /> : page === "inventory-summary" ||
            (role === "Inventory Staff" &&
              user.permissions.includes("inventory.manage") &&
              page === "dashboard") ? (
              <InventorySummary key={panelVersion} />
            ) : page === "administration" ? (
              <Administration
                key={panelVersion}
                mode={section === "audit" ? "audit" : "permissions"}
              />
            ) : page === "monitoring" ? (
              <Monitoring key={panelVersion} />
            ) : page === "analytics" ? (
              <Analytics key={panelVersion} />
            ) : page === "security" ? (
              <>
                <PasswordManagement
                  administrator={role === "Administrator"}
                  mode={role === "Customer" ? "security" : "both"}
                  onChanged={() => setUser(null)}
                />
              </>

            ) : page === "customers" && role === "Customer" ? (
              <>
                <CustomerProfile
                  customer={ownCustomer}
                  loading={busy}
                  onEdit={() => ownCustomer && edit(ownCustomer)}
                />
                <PasswordManagement
                  administrator={false}
                  mode="profile"
                  nameOnly
                  onChanged={() => setUser(null)}
                />
              </>
            ) : page === "dashboard" ? (
              <>
                <div className="dashboard-section-heading">
                  <h2>Operations snapshot</h2>
                  <span className="live-label">
                    <span /> Live database totals
                  </span>
                </div>
                {busy && !Object.keys(stats).length && (
                  <div
                    className="skeleton"
                    role="status"
                    aria-label="Loading dashboard"
                  >
                    <span />
                    <span />
                  </div>
                )}
                <div className="metrics dashboard-metrics">
                  {Object.entries(stats)
                    .filter(([, v]) => v !== null)
                    .map(([k, v]) => (
                      <section className="metric" key={k}>
                        <div className="metric-top">
                          <span>{k.replace(/([A-Z])/g, " $1")}</span>
                          <span className="metric-icon">
                            <Activity size={18} aria-hidden="true" />
                          </span>
                        </div>
                        <strong>{v}</strong>
                        <small>Current database totals</small>
                      </section>
                    ))}
                </div>
                <section className="panel workflow-panel">
                  <p className="eyebrow">YOUR NEXT STEP</p>
                  <h2>Keep service moving</h2>
                  <p className="muted">
                    {role === "Customer"
                      ? "Register your sites and equipment, submit an issue, and track work through completion."
                      : role === "Technician"
                        ? "Open Assigned jobs to start work, complete checklists and record repairs."
                        : role === "Inventory Staff"
                          ? "Maintain part levels and issue stock against in-progress work orders."
                          : "Review requests, prioritize work and assign available technicians."}
                  </p>
                  <details className="workflow-disclosure" open>
                    <summary>Service workflow</summary>
                    <div className="workflow">
                      Request <span>→</span> Assignment <span>→</span> Service{" "}
                      <span>→</span> Confirmation
                    </div>
                  </details>
                  <div className="quick-links">
                    {navigation
                      .filter((n) =>
                        [
                          "requests",
                          "work-orders",
                          "inventory/parts",
                          "analytics",
                        ].includes(n.id),
                      )
                      .slice(0, 3)
                      .map((n) => (
                        <button key={n.id} onClick={() => selectSection(n.id)}>
                          <n.icon size={17} />
                          {n.label}
                          <ArrowUpRight size={15} />
                        </button>
                      ))}
                  </div>
                </section>
              </>
            ) : (
              <section className="panel">
                <div className="table-toolbar">
                  <h2>{filtered.length} records</h2>
                  {page === "inventory/parts" && <><label>Stock filter<select value={stockFilter} onChange={e=>{setStockFilter(e.target.value);setRecordPage(1)}}><option value="all">All stock</option><option value="low">Low stock</option></select></label><label>Sort parts<select value={inventorySort} onChange={e=>{setInventorySort(e.target.value);setRecordPage(1)}}><option value="name">Part name A–Z</option><option value="stock">Lowest stock first</option></select></label></>}

                  {page === "notifications" && (
                    <select
                      aria-label="Notification filter"
                      value={notificationFilter}
                      onChange={(e) => {
                        setNotificationFilter(e.target.value);
                        setRecordPage(1);
                      }}
                    >
                      <option value="all">All notifications</option>
                      <option value="unread">Unread notifications</option>
                      <option value="read">Read notifications</option>
                    </select>
                  )}
                  <input
                    aria-label="Search records"
                    placeholder="Search records…"
                    value={search}
                    onChange={(e) => {
                      setSearch(e.target.value);
                      setRecordPage(1);
                    }}
                  />
                </div>
                {!busy &&
                  filtered.length > 0 &&
                  ((role === "Technician" && page === "work-orders") ||
                    (role === "Customer" &&
                      ["requests", "assets"].includes(page))) && (
                    <div
                      className="role-record-cards"
                      role="region"
                      aria-label={
                        page === "work-orders"
                          ? "Assigned work overview"
                          : page === "assets"
                            ? "Equipment overview"
                            : "Request overview"
                      }
                    >
                      {filtered
                        .slice((recordPage - 1) * 25, recordPage * 25)
                        .map((record, index) => (
                          <article
                            className="role-record-card"
                            key={String(
                              record.workOrderId ??
                                record.serviceRequestId ??
                                record.assetId ??
                                index,
                            )}
                          >
                            <p className="eyebrow">
                              {page === "work-orders"
                                ? `Assigned job #${record.workOrderId}`
                                : page === "requests"
                                  ? `Request #${record.serviceRequestId}`
                                  : "Your equipment"}
                            </p>
                            <h3>
                              {String(
                                record.issueDescription ??
                                  record.assetName ??
                                  record.name ??
                                  record.siteName ??
                                  "Service record",
                              )}
                            </h3>
                            {record.status !== undefined && (
                              <span className={`badge status-${record.status}`}>
                                {display("status", record.status)}
                              </span>
                            )}
                            {record.priority !== undefined && (
                              <p>
                                Priority: {display("priority", record.priority)}
                              </p>
                            )}
                            {record.scheduledVisit && (
                              <p>
                                Visit:{" "}
                                {display(
                                  "scheduledVisit",
                                  record.scheduledVisit,
                                )}
                              </p>
                            )}
                            <p>
                              {page === "work-orders"
                                ? "Use View job below for the checklist and status updates."
                                : page === "requests"
                                  ? "Follow assigned work through completion, then confirm and share feedback in My Service Jobs."
                                  : "Use Edit below to keep equipment details current."}
                            </p>
                          </article>
                        ))}
                    </div>
                  )}
                {busy ? (
                  <div
                    aria-label="Loading records"
                    role="status"
                    className="skeleton"
                  >
                    <span />
                    <span />
                    <span />
                  </div>
                ) : filtered.length === 0 ? (
                  <div className="empty">
                    <ServiceArtwork />
                    <h3>No records to show</h3>
                    <p>New records will appear here when they are created.</p>
                  </div>
                ) : (
                  <div
                    className="table-scroll"
                    tabIndex={0}
                    role="region"
                    aria-label="Service records"
                  >
                    <table>
                      <thead>
                        <tr>
                          {columns.map((k) => (
                            <th key={k}>{k.replace(/([A-Z])/g, " $1")}</th>
                          ))}
                          <th>Actions</th>
                        </tr>
                      </thead>
                      <tbody>
                        {filtered
                          .slice((recordPage - 1) * 25, recordPage * 25)
                          .map((r, i) => (
                            <tr key={i}>
                              {columns.map((k) => (
                                <td key={k}>
                                  {page === "inventory/parts" && k === "quantityInStock" ? <span className={`badge ${Number(r.quantityInStock)<=Number(r.lowStockLevel)?"low-stock":"in-stock"}`}>{r.quantityInStock} · {Number(r.quantityInStock)<=Number(r.lowStockLevel)?"Low stock":"In stock"}</span> : k === "status" ? (
                                    <span className={`badge status-${r[k]}`}>
                                      {display(k, r[k])}
                                    </span>
                                  ) : (
                                    display(k, r[k])
                                  )}
                                </td>
                              ))}
                              <td>
                                <div className="row-actions">
                                  {page === "users" && (
                                    <>
                                      <button onClick={() => edit(r)}>
                                        Edit
                                      </button>
                                      <button
                                        onClick={() => {
                                          if (
                                            confirm(
                                              `Set ${r.fullName} to ${r.accountStatus === "Active" ? "Inactive" : "Active"}?`,
                                            )
                                          )
                                            void action(
                                              `users/${r.userId}/status`,
                                              "PATCH",
                                              {
                                                status:
                                                  r.accountStatus === "Active"
                                                    ? "Inactive"
                                                    : "Active",
                                              },
                                            );
                                        }}
                                      >
                                        {r.accountStatus === "Active"
                                          ? "Deactivate"
                                          : "Activate"}
                                      </button>
                                    </>
                                  )}
                                  {page === "users" && r.accountStatus === "Active" && <button className="danger" onClick={() => {
                                    if (window.confirm(`Remove user ${r.fullName} (${r.email})? This deactivates the account and revokes its sessions. Historical records are retained; this is not permanent deletion.`)) void action(`users/${r.userId}`, "DELETE");
                                  }}>Remove User</button>}
                                  {([
                                    "customers",
                                    "sites",
                                    "assets",
                                    "inventory/parts",
                                  ].includes(page) ||
                                    (page === "technicians" && staff)) && (
                                    <button onClick={() => edit(r)}>
                                      Edit
                                    </button>
                                  )}
                                  {recordHref(page, r) && <button onClick={() => router.push(recordHref(page, r)!)}>{page === "work-orders" ? "View job" : "View details"}</button>}
                                  {page === "requests" && (
                                    <button
                                      onClick={() =>
                                        router.push(recordHref("requests", r)!)
                                      }
                                    >
                                      Evidence
                                    </button>
                                  )}
                                  {page === "requests" && staff && (
                                    <>
                                      {r.status === 0 && (
                                        <button
                                          onClick={() => router.push(recordHref("requests", r, true)!)}
                                        >
                                          Edit issue
                                        </button>
                                      )}
                                      <button
                                        onClick={() =>
                                          setForm({
                                            path: `requests/${r.serviceRequestId}/priority`,
                                            method: "PATCH",
                                            fields: [
                                              {
                                                key: "priority",
                                                label: "Priority",
                                                type: "number",
                                                required: true,
                                                options: priorities.map(
                                                  (p, i) => ({
                                                    value: String(i),
                                                    label: p,
                                                  }),
                                                ),
                                              },
                                            ],
                                          })
                                        }
                                      >
                                        Priority
                                      </button>
                                      {r.status === 0 && (
                                        <button
                                          onClick={async () => {
                                            try {
                                              const opts = await options(
                                                "technicians",
                                                "technicianId",
                                                "fullName",
                                              );
                                              setForm({
                                                path: `requests/${r.serviceRequestId}/assign`,
                                                fields: [
                                                  {
                                                    key: "technicianId",
                                                    label: "Technician",
                                                    type: "number",
                                                    required: true,
                                                    options: opts,
                                                  },
                                                  {
                                                    key: "scheduledVisit",
                                                    label: "Scheduled visit",
                                                    type: "datetime-local",
                                                  },
                                                  {
                                                    key: "jobNote",
                                                    label: "Job note",
                                                  },
                                                ],
                                              });
                                            } catch (e) {
                                              setError((e as Error).message);
                                            }
                                          }}
                                        >
                                          Assign
                                        </button>
                                      )}
                                    </>
                                  )}
                                  {page === "customers" && staff && (
                                    <button
                                      onClick={() => {
                                        selectSection("maintenance");
                                        setSearch(String(r.customerName));
                                      }}
                                    >
                                      View service history
                                    </button>
                                  )}
                                  {page === "assets" && (
                                    <button
                                      onClick={() => {
                                        selectSection("maintenance");
                                        setSearch(String(r.assetName));
                                      }}
                                    >
                                      View equipment history
                                    </button>
                                  )}
                                  {page === "requests" && r.workOrderId && (
                                    <button
                                      onClick={() => router.push(recordHref("work-orders", r)!)}
                                    >
                                      View job
                                    </button>
                                  )}
                                  {page === "inventory/parts" && (
                                    <>
                                      <button
                                        onClick={() =>
                                          setForm({
                                            path: `inventory/parts/${r.sparePartId}/add`,
                                            fields: [
                                              {
                                                key: "quantity",
                                                label: "Quantity",
                                                type: "number",
                                                required: true,
                                              },
                                              {
                                                key: "remarks",
                                                label: "Reason",
                                                required: true,
                                              },
                                            ],
                                          })
                                        }
                                      >
                                        Add stock
                                      </button>
                                      <button
                                        onClick={() =>
                                          setForm({
                                            path: `inventory/parts/${r.sparePartId}/adjust`,
                                            fields: [
                                              {
                                                key: "delta",
                                                label: "Stock adjustment (+/-)",
                                                type: "number",
                                                required: true,
                                              },
                                              {
                                                key: "remarks",
                                                label: "Adjustment reason",
                                                required: true,
                                              },
                                            ],
                                          })
                                        }
                                      >
                                        Adjust
                                      </button>
                                      <button
                                        onClick={() => openIssue(r, "return")}
                                      >
                                        Return
                                      </button>
                                      <button
                                        onClick={() => openIssue(r, "consume")}
                                      >
                                        Consume
                                      </button>
                                      <button onClick={() => openIssue(r)}>
                                        Issue
                                      </button>
                                    </>
                                  )}
                                  {page === "notifications" && (
                                    <button
                                      onClick={() =>
                                        action(
                                          `notifications/${r.notificationId}/state`,
                                          "PATCH",
                                          { isRead: !r.isRead },
                                        )
                                      }
                                    >
                                      {r.isRead ? "Mark unread" : "Mark read"}
                                    </button>
                                  )}
                                </div>
                              </td>
                            </tr>
                          ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </section>
            )}
            {requestEvidence !== null && page === "requests" && (
              <section className="panel">
                <button onClick={() => setRequestEvidence(null)}>
                  Close evidence
                </button>
                <Attachments resource="requests" id={requestEvidence} />
              </section>
            )}
            {![
              "dashboard",
              "analytics",
              "administration",
              "security",
              "inventory-summary",
            ].includes(page) &&
              filtered.length > 25 && (
                <div className="pagination">
                  <button
                    disabled={recordPage === 1}
                    onClick={() => setRecordPage((p) => p - 1)}
                  >
                    Previous records
                  </button>
                  <span>
                    Page {recordPage} of {Math.ceil(filtered.length / 25)}
                  </span>
                  <button
                    disabled={recordPage * 25 >= filtered.length}
                    onClick={() => setRecordPage((p) => p + 1)}
                  >
                    Next records
                  </button>
                </div>
              )}

            <footer>ServiceOps · Field Service & Maintenance Management</footer>
          </main>
        </div>
      </div>
    </>
  );
}
