﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Xml.Linq;
using System.Xml;
using System.Security.Cryptography;


namespace runnerSvc
{
    public partial class runnerService : ServiceBase
    {

        private RunnerServiceConfiguration sConfig; // Configuración del servicio.
        private webappDBEntities webDB;  // Conexión con DB de la webApp.
        private Thread tListener;           // Thread para el listener.
        private ResultsListener resultsListener; // Objeto que recibirá los resultados de las simulaciones

        public runnerService()
        {
            InitializeComponent();
            if (!System.Diagnostics.EventLog.SourceExists("runnerSource"))
            {
                System.Diagnostics.EventLog.CreateEventSource(
                    "runnerSource", "runnerLog");
            }
            
            // Configuramos el registro de eventos del servicio
            runner_eventLog.Source = "runnerSource";
            runner_eventLog.Log = "runnerLog";

            sConfig = RunnerServiceConfiguration.Deserialize();
        }

        protected override void OnStart(string[] args)
        {
            // Inicializamos las DB
            webDB = new webappDBEntities();


            runner_eventLog.WriteEntry("Initializing...");
            
            // Inicializamos el Listener donde recibiremos los resultados de las simulaciones
            resultsListener = new ResultsListener(webDB,sConfig,runner_eventLog);

            // Inicializamos el serverSocket donde recibir los resultados de las simulaciones
            tListener = new Thread(new ThreadStart(resultsListener.StartListening));
            tListener.Start();

            // Comprobamos simulaciones Run y las establecemos en ToRun
            try
            {
                EstadoSimulacion RunState = webDB.EstadoSimulacion.Where(s => s.Nombre.Equals("Run")).Single();
                EstadoSimulacion ToRunState = webDB.EstadoSimulacion.Where(s => s.Nombre.Equals("ToRun")).Single();
                List<Simulacion> simRunning = webDB.Simulacion.Where(s => s.IdEstadoSimulacion.Equals(RunState.IdEstadoSimulacion)).ToList<Simulacion>();
                //List<Simulacion> simRunning = webDB.Simulacion.Where(s => s.EstadoSimulacion.Equals(RunState)).ToList<Simulacion>();

                foreach (Simulacion s in simRunning)
                {
                    s.IdEstadoSimulacion = ToRunState.IdEstadoSimulacion;
                }
                webDB.SaveChanges();
                
                runner_eventLog.WriteEntry("[CHECK SIM] " + simRunning.Count);

                
                // Configuramos el timer
                timerUpdateSimulations = new System.Threading.Timer(
                    new System.Threading.TimerCallback(timerUpdateSimulations_Elapsed),
                    null,
                    1000,
                    1000
                    );               
                  
            }
            catch (Exception e)
            {
                runner_eventLog.WriteEntry("[CHECK SIM] Error: "+e);
                this.Stop();
            }                   

        }

        protected override void OnStop()
        {
            timerUpdateSimulations.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
            timerUpdateSimulations.Dispose();
            timerUpdateSimulations = null;


            
            runner_eventLog.WriteEntry("Stopping...");
        }

        private void timerUpdateSimulations_Elapsed(object state)
        {
            //runner_eventLog.WriteEntry("[UPDATE SIMULATIONS] New tick at "+DateTime.Now);
            
            EstadoSimulacion runState = webDB.EstadoSimulacion.Where(s => s.Nombre.Equals("Run")).Single();
            EstadoSimulacion toRunState = webDB.EstadoSimulacion.Where(s => s.Nombre.Equals("ToRun")).Single();

            // Consultamos las simulaciones ejecutándose por usuario.
            Dictionary<String, int> simByUser = new Dictionary<String, int>();
            List<String> usersSimRunning = webDB.Simulacion.Where(sr => sr.IdEstadoSimulacion.Equals(runState.IdEstadoSimulacion)).Select(sr => sr.Usuario).ToList();
            foreach (String u in usersSimRunning)
            {
                if (!simByUser.ContainsKey(u))
                {
                    simByUser.Add(u, 1);
                }
                else
                {
                    simByUser[u] = simByUser[u] + 1;
                }
            }

            // Consultamos las simulaciones que se encuentran en el estado ToRun.            
            List<Simulacion> simToRun = webDB.Simulacion.Where(s => s.IdEstadoSimulacion.Equals(toRunState.IdEstadoSimulacion)).ToList<Simulacion>();
            //runner_eventLog.WriteEntry("[SIMULATIONS QUEUE] " + simToRun.Count());

            foreach (Simulacion s in simToRun)
            {
                // Descartamos aquellas que superen el máximo de simulaciones por usuario MAXUSER.
                if (simByUser.ContainsKey(s.Usuario) && simByUser[s.Usuario] > sConfig.MaxUsers)
                {
                    runner_eventLog.WriteEntry("[UPDATE SIMULATIONS] " + s.Usuario + ": Too simulations. Denegate");
                }
                else
                {
                    // Establecemos dichas simulaciones a Run, es decir, las lanzamos.           
                    s.EstadoSimulacion = runState;
                    try
                    {
                        webDB.SaveChanges();
                        runner_eventLog.WriteEntry("[UPDATE SIMULATIONS] Launching simulation");
                        runSimulation(s.IdSimulacion);
                    }
                    catch (Exception e)
                    {
                        runner_eventLog.WriteEntry("[UPDATE SIMULATIONS] Update error: " + e.ToString());
                    }

                }
            }


            simByUser = null;
        }
        
        private void runSimulation(Guid idSimulacion)
        {
            runner_eventLog.WriteEntry("[RUN SIMULATION] "+idSimulacion);

            // Obtenemos los datos de la simulación así como el archivo de datos del proyecto
            Simulacion simulacion = webDB.Simulacion.Where(s => s.IdSimulacion.Equals(idSimulacion)).Single();
            Archivo archivoDatos = webDB.Archivo
                .Where(a => 
                    a.IdArchivo.Equals(simulacion.IdArchivo))            
                .Single();

            // Creamos un documento XML con los datos a enviar
            runner_eventLog.WriteEntry("[RUN SIMULATION] Building XML...");

            XDocument datosXML = new XDocument(
                new XDeclaration("1.0", "utf-8","yes"),
                new XComment("Simulacion"),
                new XElement("Simulacion",
                    new XElement("IdSimulacion", simulacion.IdSimulacion.ToString()),
                    new XElement("IdProyecto", simulacion.IdProyecto.ToString()),
                    new XElement("Nombre", simulacion.Nombre),
                    new XElement("Descripcion",simulacion.Descripcion),
                    new XElement("FechaCreacionSimulacion",simulacion.FechaCreacionSimulacion.ToString("yyyy-mm-ddThh:mm:ss")),
                    new XElement("IdEstadoSimulacion", simulacion.IdEstadoSimulacion.ToString()),
                    new XElement("IdMetodoClasificacion", simulacion.IdMetodoClasificacion.ToString()),
                    new XElement("IdMetodoSeleccion",simulacion.IdMetodoSeleccion.ToString()),
                    new XElement("ParametrosClasificacion",Simulacion.ParseClasificationParameters(simulacion.ParametrosClasificacion)),
                    new XElement("ParametrosSeleccion",Simulacion.ParseSelectionParameters(simulacion.ParametrosSeleccion)),
                    new XElement("Usuario",simulacion.Usuario),
                    new XElement("Datos", archivoDatos.Datos)
                )
                
            );

            // Convertimos a bytes el XML, para ello antes inclumos "<EOF>" como final del archivo
            byte[] bytesXML = Encoding.ASCII.GetBytes(datosXML.ToString()+"<EOF>");            
            

            /// NUEVA FORMA DE ENVIAR http://msdn.microsoft.com/es-es/library/bew39x2a(v=vs.80).aspx
            Socket clientSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                
                // Configuramos la conexión
                runner_eventLog.WriteEntry("[SEND SIMULATION] Try to connect...");
                clientSock.Connect(sConfig.IpDaemon, sConfig.PortDaemon); 
                
                // Enviamos los datos
                runner_eventLog.WriteEntry("[SEND SIMULATION] Connected. Starting to send" + bytesXML.Length + " bytes");
                clientSock.Send(bytesXML);
                
                // Esperamos confirmación
                // TODO Recibir confirmación
                byte[] rBytes = new byte[1024];
                int raw = clientSock.Receive(rBytes);

                // Mostramos confirmación
                // TODO Decidir que vamos a hacer cuando veamos que no ha llegado bien
                runner_eventLog.WriteEntry("[SEND SIMULATION]Confirmation: Sended: " + bytesXML.Length + " Received: "+Encoding.ASCII.GetString(rBytes));

                // Cerramos la conexión
                clientSock.Close();
                runner_eventLog.WriteEntry("[SEND SIMULATION] Sended.");

                // Establecemos a Running la simulación
                webDB.Simulacion.Where(s => s.IdSimulacion.Equals(simulacion.IdSimulacion))
                    .Single()
                    .IdEstadoSimulacion = webDB.EstadoSimulacion
                                                            .Where(es => es.Nombre.Equals("Run"))
                                                            .Single()
                                                            .IdEstadoSimulacion;
                webDB.SaveChanges();
            }
            catch (System.TimeoutException error)
            {
                runner_eventLog.WriteEntry("[SEND SIMULATION] Timeout finished: " + error);
            }
            catch (SocketException se)
            {
                runner_eventLog.WriteEntry("[SEND SIMULATION] Cannot connect to remote host: " + se.Message);
            }
            catch (Exception error)
            {
                runner_eventLog.WriteEntry("[SEND SIMULATION] Error desconocido: " + error);
            }    

        }
    }
}
